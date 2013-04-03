#region license
// /*
//     This file is part of Naming Fix AddIn.
// 
//     Naming Fix AddIn is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
// 
//     Naming Fix AddIn is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
// 
//     You should have received a copy of the GNU General Public License
//     along with Naming Fix AddIn. If not, see <http://www.gnu.org/licenses/>.
//  */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using EnvDTE;
using EnvDTE80;

namespace NamingFix
{
    class CNamingFix
    {
        private struct SWorkStatus
        {
            public int MainValue;
            public int SubValue, SubMax;
            public string Text { get; private set; }
            public string Exception;

            public void SetText(string text, bool addMain = true)
            {
                Text = text;
                SubValue = 0;
                if (addMain)
                    MainValue++;
            }
        }

        private const int Priv = 0;
        private const int Prot = 1;
        private const int Pub = 2;
        private readonly DTE2 _Dte;
        private static OutputWindowPane _OutputWindow;
        private CRenameRuleSet _RuleSet;
        private static readonly Regex _ReUnderScore = new Regex("(?<=[a-z0-9])_[a-z0-9]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex _ReMultiCaps = new Regex("[A-Z]{2,}", RegexOptions.Compiled);
        private static readonly Regex _ReEndsWithNoCaps = new Regex("[a-z]$", RegexOptions.Compiled);

        private CRenameItemClass _RenameItems;
        private readonly Dictionary<string, CRenameItemInterfaceBase> _SysClassCache = new Dictionary<string, CRenameItemInterfaceBase>();

        public static readonly List<String> FoundTypes = new List<string>();

        private static readonly CTypeResolver _TypeResolver = new CTypeResolver();
        private static readonly Form1 _StatusForm = new Form1();
        private static int _ElCount;
        private const string _TmpPrefix = "RNFTMPPRE";
        private System.Threading.Thread _WorkerThread;
        private static SWorkStatus _WorkStatus;
        private readonly Timer _UpdateTimer = new Timer();

        //Strings
        //comments
        public CNamingFix(DTE2 dte)
        {
            _Dte = dte;
            Window window = _Dte.Windows.Item(Constants.vsWindowKindOutput);
            OutputWindow outputWindow = (OutputWindow)window.Object;
            _OutputWindow = outputWindow.OutputWindowPanes.Add("new pane");
            _StatusForm.pbMain.Maximum = 5;
            _UpdateTimer.Interval = 30;
            _UpdateTimer.Tick += Timer_ShowStatus;
        }

        /// <summary>
        ///     This function is the callback used to execute a command when the a menu item is clicked.
        ///     See the Initialize method to see how the menu item is associated to this function using
        ///     the OleMenuCommandService service and the MenuCommand class.
        /// </summary>
        public void MenuItemCallback(object sender, EventArgs e)
        {
            if (_WorkerThread != null)
                return;
            _OutputWindow.Clear();
            _WorkStatus.MainValue = 0;
            _WorkStatus.SetText("Initializing", false);
            _WorkStatus.Exception = "";
            ShowStatus();
            _StatusForm.Show();
            _WorkerThread = new System.Threading.Thread(Execute);
            _WorkerThread.Start();
            _UpdateTimer.Start();
        }

        private void Timer_ShowStatus(object sender, EventArgs e)
        {
            if (!_WorkerThread.IsAlive)
            {
                _StatusForm.Hide();
                _UpdateTimer.Stop();
                _WorkerThread = null;
                if (_WorkStatus.Exception != "")
                    MessageBox.Show(_WorkStatus.Exception, "Exception occured", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
                ShowStatus();
        }

        private void ShowStatus()
        {
            _StatusForm.pbMain.Value = _WorkStatus.MainValue;
            int value = _WorkStatus.SubValue;
            _StatusForm.pbSub.Maximum = _WorkStatus.SubMax;
            _StatusForm.pbSub.Value = value;
            _StatusForm.lblText.Text = _WorkStatus.Text;
        }

        private void Execute()
        {
            _Dte.UndoContext.Open("Item renaming");
            try
            {
                _TypeResolver.AddTypes();
                BuildClassTree();
                _RuleSet = new CRenameRuleSet();
                SetNewNames();
                CheckChanges();


                Message("\r\nFound local var types:");
                foreach (string type in FoundTypes.Where(type => !_TypeResolver.IsType(type)))
                    Message(type);
                Message("IMPORTANT: Check if all found types above are actual types otherwhise use undo and fix addin!");
            }
            catch (Exception exception)
            {
                _WorkStatus.Exception = "Exception occured!:\r\n" + exception.Message;
            }
            _Dte.UndoContext.Close();
        }

        #region BuildClassTree
        private static bool CountElems(CRenameItem item)
        {
            _ElCount++;
            return true;
        }

        private void BuildClassTree()
        {
            _WorkStatus.SetText("Gathering classes");
            _RenameItems = new CRenameItemClass {IsTopClass = true};
            _SysClassCache.Clear();
            _WorkStatus.SubMax = _Dte.Solution.Projects.Count * 101;
            foreach (Project project in _Dte.Solution.Projects)
            {
                _WorkStatus.SubMax += project.ProjectItems.Count - 100;
                IterateProjectItems(project.ProjectItems);
                _WorkStatus.SubValue++;
            }
            _WorkStatus.SetText("Gathering class inheritance info");
            _WorkStatus.SubMax = 0;
            _ElCount = 0;
            TraverseItems(CountElems);
            AddInheritedInfoToMembers(_RenameItems);
        }

        private void IterateProjectItems(ProjectItems projectItems)
        {
            if (projectItems == null)
                return;
            foreach (ProjectItem item in projectItems)
            {
                if (item.Kind == Constants.vsProjectItemKindPhysicalFile && item.Name.EndsWith(".cs") &&
                    item.FileCodeModel.Language == CodeModelLanguageConstants.vsCMLanguageCSharp)
                {
                    Message("File " + item.Name + ":");
                    IterateCodeElements(item.FileCodeModel.CodeElements, _RenameItems);
                }
                ProjectItems subItems = item.SubProject != null ? item.SubProject.ProjectItems : item.ProjectItems;
                if (subItems != null)
                {
                    _WorkStatus.SubMax += subItems.Count;
                    IterateProjectItems(subItems);
                }
                _WorkStatus.SubValue++;
            }
        }

        //Iterate through all the code elements in the provided element
        private static void IterateCodeElements(CodeElements colCodeElements, IRenameItemContainer curParent)
        {
            //Check for nonmutable object inheritance
            if (colCodeElements == null)
                return;
            foreach (CodeElement element in colCodeElements)
            {
                try
                {
                    CRenameItemElement cItem = null;
                    switch (element.Kind)
                    {
                        case vsCMElement.vsCMElementVariable:
                            cItem = new CRenameItemVariable();
                            break;
                        case vsCMElement.vsCMElementProperty:
                            cItem = new CRenameItemProperty();
                            break;
                        case vsCMElement.vsCMElementFunction:
                            cItem = new CRenameItemMethod();
                            IterateCodeElements(((CodeFunction2)element).Parameters, (IRenameItemContainer)cItem);
                            break;
                        case vsCMElement.vsCMElementParameter:
                            cItem = new CRenameItemParameter();
                            break;
                        case vsCMElement.vsCMElementInterface:
                            cItem = new CRenameItemInterface();
                            IterateCodeElements(((CodeInterface2)element).Members, (IRenameItemContainer)cItem);
                            break;
                        case vsCMElement.vsCMElementEnum:
                            cItem = new CRenameItemEnum();
                            break;
                        case vsCMElement.vsCMElementStruct:
                            cItem = new CRenameItemStruct();
                            break;
                        case vsCMElement.vsCMElementNamespace:
                            CodeNamespace objCodeNamespace = (CodeNamespace)element;
                            IterateCodeElements(objCodeNamespace.Members, curParent);
                            break;
                        case vsCMElement.vsCMElementClass:
                            cItem = new CRenameItemClass();
                            IterateCodeElements(((CodeClass2)element).Members, (IRenameItemContainer)cItem);
                            break;
                        case vsCMElement.vsCMElementDelegate:
                            cItem = new CRenameItemDelegate();
                            break;
                        case vsCMElement.vsCMElementEvent:
                            cItem = new CRenameItemEvent();
                            break;
                        case vsCMElement.vsCMElementImportStmt:
                        case vsCMElement.vsCMElementAttribute:
                            break;
                        default:
                            Message("Unhandled element kind: " + element.Kind.ToString());
                            break;
                    }
                    if (cItem == null)
                        continue;
                    cItem.Element = element;
                    curParent.Add(cItem);
                }
                catch {}
            }
        }

        private void AddInheritedInfoToMembers(CRenameItemClass child)
        {
            _WorkStatus.SubMax += child.Classes.Count + child.Interfaces.Count;
            foreach (CRenameItemClass item in child.Classes)
            {
                GetInherited(item);
                _WorkStatus.SubValue++;
            }
            foreach (CRenameItemInterface item in child.Interfaces)
            {
                GetInherited(item);
                _WorkStatus.SubValue++;
            }
        }

        private void GetInherited(CRenameItemClass child)
        {
            if (child.IsInheritedLoaded)
                return;
            Message("Get inherited: " + child.Name);
            foreach (CodeElement baseClass in child.GetElement().Bases)
                ProcessBaseElement<CRenameItemClass>(baseClass, child);
            foreach (CodeElement implInterface in child.GetElement().ImplementedInterfaces)
                ProcessBaseElement<CRenameItemInterface>(implInterface, child);
            child.IsInheritedLoaded = true;
            AddInheritedInfoToMembers(child);
        }

        private void GetInherited(CRenameItemInterface child)
        {
            if (child.IsInheritedLoaded)
                return;
            Message("Get inherited: " + child.Name);
            foreach (CodeElement element in child.GetElement().Bases)
                ProcessBaseElement<CRenameItemInterface>(element, child);
            child.IsInheritedLoaded = true;
        }

        private void ProcessBaseElement<T>(CodeElement element, CRenameItemInterfaceBase child) where T : CRenameItemInterfaceBase, new()
        {
            //Look for (programmer-defined) project class
            T baseType = (T)child.FindTypeByName(element.Name);
            if (baseType == null)
            {
                //Class is system or extern class
                CRenameItemInterfaceBase tmp;
                if (_SysClassCache.TryGetValue(element.Name, out tmp))
                    baseType = (T)tmp;
                else
                {
                    baseType = new T {Element = element, Name = element.Name, IsSystem = true};
                    if (element.Kind == vsCMElement.vsCMElementClass)
                        IterateCodeElements(((CodeClass2)element).Members, baseType);
                    else
                        IterateCodeElements(((CodeInterface2)element).Members, baseType);
                    _SysClassCache.Add(element.Name, baseType);
                }
            }

            //set derivedinfo
            baseType.CopyIdsDerived(child);

            //Make sure, inheritance info is filled
            CRenameItemClass tmpClass = baseType as CRenameItemClass;
            if (tmpClass != null)
                GetInherited(tmpClass);
            else
                GetInherited(baseType as CRenameItemInterface);
            //Get all Ids from class and its parents
            child.CopyIds(baseType);
        }
        #endregion

        #region GetNewName
        private string GetNewName(CodeFunction2 func)
        {
            return GetNewName((CodeElement)func, _RuleSet.Method, func.Access);
        }

        private string GetNewName(CodeVariable2 variable)
        {
            if (variable.IsConstant)
                return GetNewName((CodeElement)variable, _RuleSet.Const, variable.Access);
            return GetNewName((CodeElement)variable, _RuleSet.Field, variable.Access);
        }

        private string GetNewName(CodeProperty2 property)
        {
            return GetNewName((CodeElement)property, _RuleSet.Property, property.Access);
        }

        private static string GetNewName(CodeElement element, SRenameRule[] rules, vsCMAccess access)
        {
            switch (access)
            {
                case vsCMAccess.vsCMAccessPrivate:
                    return GetNewName(element, rules[Priv]);
                case vsCMAccess.vsCMAccessProtected:
                    return GetNewName(element, rules[Prot]);
                case vsCMAccess.vsCMAccessPublic:
                    return GetNewName(element, rules[Pub]);
            }
            Message("Found unknown access modifier for " + element.Name + " " + access);
            return element.Name;
        }

        private static string GetNewName(CodeElement element, SRenameRule rule)
        {
            return GetNewName(element.Name, rule);
        }

        private static string GetNewName(string theString, SRenameRule rule)
        {
            if (rule.DontChange)
                return theString;

            RemovePrefix(ref theString, rule.RemovePrefix);
            RemovePrefix(ref theString, rule.Prefix);

            switch (rule.NamingStyle)
            {
                case
                    ENamingStyle.LowerCamelCase:
                    if (rule.Prefix != null && _ReEndsWithNoCaps.IsMatch(rule.Prefix))
                        ToUpperCamelCase(ref theString);
                    else
                        ToLowerCamelCase(ref theString);
                    break;
                case ENamingStyle.UpperCamelCase:
                    ToUpperCamelCase(ref theString);
                    break;
                case ENamingStyle.UpperCase:
                    theString = theString.ToUpper();
                    break;
                case ENamingStyle.LowerCase:
                    theString = theString.ToLower();
                    break;
            }
            AddPrefix(ref theString, rule.Prefix);

            return theString;
        }

        private static void RemovePrefix(ref string theString, string prefix)
        {
            if (string.IsNullOrEmpty(prefix))
                return;
            if (theString.Length <= prefix.Length || !theString.StartsWith(prefix))
                return;
            //Do not remove if prexix ends with a capital and next char is no capital
            //In that case it is likely that the prefix is part of the name. e.g. prefix "C" and name "class CodeBook"
            if (_ReEndsWithNoCaps.IsMatch(prefix) || !_ReEndsWithNoCaps.IsMatch(theString.Substring(prefix.Length, 1)))
                theString = theString.Remove(0, prefix.Length);
        }

        private static void ToLowerCamelCase(ref String theString)
        {
            ToCamelCase(ref theString);
            theString = theString.Substring(0, 1).ToLower() + theString.Substring(1);
        }

        private static void ToUpperCamelCase(ref String theString)
        {
            theString = theString.Substring(0, 1).ToUpper() + theString.Substring(1);
            ToCamelCase(ref theString);
        }

        private static void ToCamelCase(ref String theString)
        {
            theString = theString.Replace("__", "_");
            theString = _ReMultiCaps.Replace(theString, m => m.Value[0] + m.Value.Substring(1).ToLower());
            theString = _ReUnderScore.Replace(theString, m => m.Value[1].ToString().ToUpper());
        }

        private static void AddPrefix(ref String theString, string prefix = "")
        {
            if (String.IsNullOrEmpty(prefix))
                return;
            if (!theString.StartsWith(prefix))
                theString = prefix + theString;
        }
        #endregion

        #region SetNewNames
        private static void SetNewName(CRenameItem item, SRenameRule rule)
        {
            item.NewName = GetNewName(item.Name, rule);
        }

        private bool SetNewName(CRenameItem item)
        {
            // ReSharper disable CanBeReplacedWithTryCastAndCheckForNull
            _WorkStatus.SubValue++;
            if (item.IsSystem)
                item.NewName = item.Name;
            else if (item is CRenameItemClass)
                SetNewName(item, _RuleSet.Class);
            else if (item is CRenameItemInterface)
                SetNewName(item, _RuleSet.Interface);
            else if (item is CRenameItemEnum)
                SetNewName(item, _RuleSet.Enum);
            else if (item is CRenameItemStruct)
                SetNewName(item, _RuleSet.Struct);
            else if (item is CRenameItemEvent)
                SetNewName(item, _RuleSet.Event);
            else if (item is CRenameItemLocVar)
            {
                if (((CRenameItemLocVar)item).IsConst)
                    SetNewName(item, _RuleSet.LokalConst);
                else
                    SetNewName(item, _RuleSet.LokalVariable);
            }
            else if (item is CRenameItemVariable)
                item.NewName = GetNewName(((CRenameItemVariable)item).GetElement());
            else if (item is CRenameItemProperty)
                item.NewName = GetNewName(((CRenameItemProperty)item).GetElement());
            else if (item is CRenameItemParameter)
                SetNewName(item, _RuleSet.Parameter);
            else if (item is CRenameItemMethod)
                item.NewName = GetNewName(((CRenameItemMethod)item).GetElement());
            return true;
            // ReSharper restore CanBeReplacedWithTryCastAndCheckForNull
        }

        private void SetNewNames()
        {
            _WorkStatus.SetText("Calculating new names");
            _WorkStatus.SubMax = _ElCount;
            TraverseItems(SetNewName);
        }
        #endregion

        private bool ApplyChangesPre(CRenameItem item)
        {
            _WorkStatus.SubValue++;
            if (!(item is CRenameItemLocVar))
            {
                if (item.Name != item.NewName)
                {
                    item.NewName = item.GetTypeName() + _TmpPrefix + item.NewName;
                    item.Rename();
                }
            }
            return true;
        }

        private bool ApplyChangesLocalVar(CRenameItem item)
        {
            _WorkStatus.SubValue++;
            CRenameItemMethod method = item as CRenameItemMethod;
            if (method != null)
            {
                method.ReloadText();
                foreach (var localVar in method.LocalVars)
                    localVar.Rename();
                method.ApplyNewText();
            }
            return true;
        }

        private bool ApplyChangesPost(CRenameItem item)
        {
            _WorkStatus.SubValue++;
            if (!(item is CRenameItemLocVar))
            {
                if (item.Name != item.NewName)
                {
                    item.NewName = item.NewName.Substring(item.NewName.IndexOf(_TmpPrefix, StringComparison.Ordinal));
                    item.Rename();
                }
            }
            return true;
        }

        private bool ApplyChanges()
        {
            _WorkStatus.SubMax = _ElCount * 3;
            return TraverseItems(ApplyChangesPre) &&
                   TraverseItems(ApplyChangesLocalVar) &&
                   TraverseItems(ApplyChangesPost);
        }

        private bool CheckChanges(CRenameItem item)
        {
            _WorkStatus.SubValue++;
            if (item.IsRenameValid())
                return true;
            string name = item.Name;
            if (!string.IsNullOrEmpty(item.Parent.Name))
                name = item.Parent.Name + "." + name;
            Message("Cannot rename " + item.GetTypeName() + " " + name + " to " + item.NewName +
                    " as another identifier with the same name already exists or is about to be renamed to the same name!");
            if (item.ProjectItem.Document == null || item.ProjectItem.Document.Windows.Count == 0)
                item.ProjectItem.Open(Constants.vsViewKindCode);
            item.StartPoint.TryToShow(vsPaneShowHow.vsPaneShowTop);
            return true;
        }

        private bool CheckChanges()
        {
            _WorkStatus.SetText("Validating changes");
            return TraverseItems(CheckChanges);
        }

        private delegate bool HandleItem(CRenameItem item);

        private bool TraverseItems(HandleItem callBack)
        {
            return TraverseItems(_RenameItems, callBack);
        }

        private bool TraverseItems(IRenameItemContainer itemContainer, HandleItem callBack)
        {
            CRenameItemMethod itemMethod = itemContainer as CRenameItemMethod;
            if (itemMethod != null)
            {
                if (!TraverseItems(itemMethod.Parameters, callBack))
                    return false;
                if (!TraverseItems(itemMethod.LocalVars, callBack))
                    return false;
            }
            else
            {
                CRenameItemInterfaceBase itemInterface = (CRenameItemInterfaceBase)itemContainer;
                CRenameItemClass itemClass = itemInterface as CRenameItemClass;
                if (itemClass != null)
                {
                    if (!TraverseItems(itemClass.Classes, callBack) ||
                        !TraverseItems(itemClass.Interfaces, callBack) ||
                        !TraverseItems(itemClass.Enums, callBack) ||
                        !TraverseItems(itemClass.Structs, callBack) ||
                        !TraverseItems(itemClass.Variables, callBack) ||
                        !TraverseItems(itemClass.Delegates, callBack))
                        return false;
                }
                if (!TraverseItems(itemInterface.Properties, callBack) ||
                    !TraverseItems(itemInterface.Methods, callBack))
                    return false;
            }
            return true;
        }

        private bool TraverseItems<T>(IEnumerable<T> list, HandleItem callBack) where T : CRenameItem
        {
            foreach (T item in list)
            {
                if (!callBack(item))
                    return false;
                IRenameItemContainer container = item as IRenameItemContainer;
                if (container != null && !TraverseItems(container, callBack))
                    return false;
            }
            return true;
        }

        public static void Message(string msg)
        {
            _OutputWindow.Activate();
            _OutputWindow.OutputString(msg + "\r\n");
        }

        private class CTypeResolver
        {
            private Dictionary<string, Type> _Alias;

            public void AddTypes()
            {
                _Alias = new Dictionary<string, Type>
                    {
                        {"bool", typeof(bool)},
                        {"byte", typeof(byte)},
                        {"sbyte", typeof(sbyte)},
                        {"char", typeof(char)},
                        {"decimal", typeof(decimal)},
                        {"double", typeof(double)},
                        {"float", typeof(float)},
                        {"int", typeof(int)},
                        {"uint", typeof(uint)},
                        {"long", typeof(long)},
                        {"ulong", typeof(ulong)},
                        {"object", typeof(object)},
                        {"short", typeof(short)},
                        {"ushort", typeof(ushort)},
                        {"string", typeof(string)}
                    };

                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                _WorkStatus.SubMax += assemblies.Length * 201;
                foreach (Assembly a in assemblies)
                {
                    try
                    {
                        Type[] types = a.GetTypes();
                        _WorkStatus.SubMax += types.Length - 200;
                        foreach (var type in types)
                        {
                            _Alias[type.Name] = type;
                            _WorkStatus.SubValue++;
                        }
                    }
                    catch {}
                    _WorkStatus.SubValue++;
                }
            }

            public bool IsType(string typeName)
            {
                if (_Alias == null)
                    AddTypes();
                // ReSharper disable PossibleNullReferenceException
                return _Alias.ContainsKey(typeName);
                // ReSharper restore PossibleNullReferenceException
            }
        }
    }
}