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

using EnvDTE;
using EnvDTE80;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace NamingFix
{
    using ItemTuple = Tuple<CRenameItem, CRenameItem>;

    class CNamingFix : IDisposable
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

        private CRenameItemNamespace _RenameItems;
        private readonly Dictionary<string, CRenameItemInterfaceBase> _SysClassCache = new Dictionary<string, CRenameItemInterfaceBase>();

        public static readonly List<String> FoundTypes = new List<string>();

        private readonly CFormStatus _FormStatus = new CFormStatus();
        private readonly CFormConflicts _FormConflicts = new CFormConflicts();
        public static readonly List<ItemTuple> Conflicts = new List<ItemTuple>();
        private static int _ElCount;
        private const string _TmpPrefix = "RNFTMPPRE";
        private System.Threading.Thread _WorkerThread;
        private static SWorkStatus _WorkStatus;
        private readonly Timer _UpdateTimer = new Timer();
        private bool _IsDisposed;

        //Strings
        //comments
        public CNamingFix(DTE2 dte)
        {
            _Dte = dte;
            _OutputWindow = GetPane("Naming Fix AddIn");
            _OutputWindow.Activate();
            _FormStatus.pbMain.Maximum = 9;
            _UpdateTimer.Interval = 80;
            _UpdateTimer.Tick += Timer_ShowStatus;
        }

        private OutputWindowPane GetPane(string name)
        {
            Window window = _Dte.Windows.Item(Constants.vsWindowKindOutput);
            OutputWindow outputWindow = (OutputWindow)window.Object;
            foreach (OutputWindowPane pane in outputWindow.OutputWindowPanes)
            {
                if (pane.Name == name)
                    return pane;
            }
            return outputWindow.OutputWindowPanes.Add(name);
        }

        /// <summary>
        ///     This function is used to execute a command when the a menu item is clicked.
        /// </summary>
        public void DoFix()
        {
            if (_WorkerThread != null)
                return;
            _OutputWindow.Clear();
            _FormConflicts.Hide();
            if (MessageBox.Show(
                "To avoid possible side effects please make sure your solution compiles completely without any errors at all!\r\n\r\nAlso make sure you have a backup or current commit.\r\n\r\nContinue?",
                "Naming Fix", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;
            _WorkStatus.MainValue = 0;
            _WorkStatus.SetText("Initializing", false);
            _WorkStatus.Exception = "";
            ShowStatus();
            _FormStatus.Show();
            _WorkerThread = new System.Threading.Thread(Execute);
            _WorkerThread.Start();
            _UpdateTimer.Start();
        }

        private void Timer_ShowStatus(object sender, EventArgs e)
        {
            if (!_WorkerThread.IsAlive)
            {
                _FormStatus.Hide();
                _UpdateTimer.Stop();
                _WorkerThread = null;
                if (_WorkStatus.Exception != "")
                    MessageBox.Show(_WorkStatus.Exception, "Exception occured", MessageBoxButtons.OK, MessageBoxIcon.Error);
                else
                    ShowConflicts();
            }
            else
                ShowStatus();
        }

        private void ShowStatus()
        {
            _FormStatus.pbMain.Value = _WorkStatus.MainValue;
            int value = _WorkStatus.SubValue;
            _FormStatus.pbSub.Maximum = _WorkStatus.SubMax;
            if (value <= _FormStatus.pbSub.Maximum)
                _FormStatus.pbSub.Value = value;
            _FormStatus.lblText.Text = _WorkStatus.Text;
        }

        private void Execute()
        {
            Message("Analysis started!");
            _Dte.UndoContext.Open("Item renaming");
            try
            {
                CTypeResolver.AddTypes();
                FoundTypes.Clear();
                Conflicts.Clear();
                if (BuildClassTree())
                {
                    CountElements();
                    GetInheritanceInfo();
                    _RuleSet = new CRenameRuleSet();
                    SetNewNames();
                    if (CheckChanges() && Conflicts.Count == 0)
                        ApplyChanges();

                    Message("\r\nFound local var types:");
                    foreach (string type in FoundTypes.Where(type => !CTypeResolver.IsType(type)))
                        Message(type);
                    Message("IMPORTANT: Check if all found types above are actual types otherwhise use undo and fix addin!");
                }
            }
            catch (Exception exception)
            {
                _WorkStatus.Exception = "Exception occured!:\r\n" + exception.Message;
            }
            _Dte.UndoContext.Close();
        }

        private static void AddParentName(ref String name, CRenameItem item)
        {
            if (string.IsNullOrEmpty(item.Parent.Name))
                return;
            if (item.Parent is CRenameItemType || item.Parent is CRenameItemNamespace)
                name = item.Parent.Name;
            else
                name += " in " + item.Parent.Name;
        }

        private void ShowConflicts()
        {
            if (Conflicts.Count == 0)
                return;
            _FormConflicts.lbConflicts.Items.Clear();
            foreach (var item in Conflicts)
            {
                string name = item.Item1.Name;
                AddParentName(ref name, item.Item1);
                string name2 = item.Item2.Name;
                if (item.Item1.Parent.Name != item.Item2.Parent.Name)
                    AddParentName(ref name2, item.Item2);
                _FormConflicts.lbConflicts.Items.Add(item.Item1.GetTypeName() + " " + name + " --> " + item.Item1.NewName + " <=> " + item.Item2.GetTypeName() + " " + name2);
            }
            _FormConflicts.Show();
        }

        #region BuildClassTree
        private bool BuildClassTree()
        {
            _WorkStatus.SetText("Gathering classes");
            _RenameItems = new CRenameItemNamespace();
            _SysClassCache.Clear();
            _WorkStatus.SubMax = _Dte.Solution.Projects.Count * 41;
            if (_Dte.Solution.Projects.Count == 0)
            {
                _WorkStatus.Exception = "No Projects to process. Please open one before applying this!";
                return false;
            }
            foreach (Project project in _Dte.Solution.Projects)
            {
                _WorkStatus.SubMax += project.ProjectItems.Count - 40;
                IterateProjectItems(project.ProjectItems);
                _WorkStatus.SubValue++;
            }
            return true;
        }

        private void IterateProjectItems(ProjectItems projectItems)
        {
            foreach (ProjectItem item in projectItems)
            {
                if (item.Kind == Constants.vsProjectItemKindPhysicalFile && item.Name.EndsWith(".cs") &&
                    item.FileCodeModel.Language == CodeModelLanguageConstants.vsCMLanguageCSharp)
                {
#if(DEBUG)
                    Message("File " + item.Name + ":");
#endif
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
        private void IterateCodeElements(CodeElements colCodeElements, IRenameItemContainer curParent)
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
                            CRenameItemNamespace ns = ((CRenameItemNamespace)curParent).AddOrGetNamespace(element.Name, element);
                            IterateCodeElements(objCodeNamespace.Members, ns);
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

        private static bool CountElems(CRenameItem item)
        {
            _ElCount++;
            return true;
        }

        private void CountElements()
        {
            _WorkStatus.SetText("Initializing and counting elements");
            _ElCount = 0;
            TraverseItemContainer(CountElems);
        }

        private void GetInheritanceInfo()
        {
            _WorkStatus.SetText("Gathering inheritance info");
            _WorkStatus.SubMax = 21;
            AddInheritedInfoToMembers(_RenameItems);
        }

        private void AddInheritedInfoToMembers(CRenameItemClass child)
        {
            _WorkStatus.SubMax += child.Classes.Count * 6 + child.Interfaces.Count - 5;
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

        private void AddInheritedInfoToMembers(CRenameItemNamespace child)
        {
            _WorkStatus.SubMax += child.Classes.Count * 6 + child.Interfaces.Count + child.Namespaces.Count * 21 - 20;
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
            foreach (CRenameItemNamespace item in child.Namespaces)
            {
                AddInheritedInfoToMembers(item);
                _WorkStatus.SubValue++;
            }
        }

        private void GetInherited(CRenameItemClass child)
        {
            if (child.IsInheritedLoaded)
                return;
#if(DEBUG)
            Message("Get inherited: " + child.Name);
#endif
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
#if(DEBUG)
            Message("Get inherited: " + child.Name);
#endif
            foreach (CodeElement element in child.GetElement().Bases)
                ProcessBaseElement<CRenameItemInterface>(element, child);
            child.IsInheritedLoaded = true;
        }

        private void ProcessBaseElement<T>(CodeElement element, CRenameItemInterfaceBase child) where T : CRenameItemInterfaceBase, new()
        {
            //Look for (programmer-defined) project class
            T baseType = (T)_RenameItems.FindTypeByName(element.FullName);
            if (baseType == null)
            {
                //Class is system or extern class
                CRenameItemInterfaceBase tmp;
                if (_SysClassCache.TryGetValue(element.FullName, out tmp))
                    baseType = (T)tmp;
                else
                {
                    baseType = new T {Element = element, Name = element.Name, IsSystem = true};
                    if (element.Kind == vsCMElement.vsCMElementClass)
                        IterateCodeElements(((CodeClass2)element).Members, baseType);
                    else
                        IterateCodeElements(((CodeInterface2)element).Members, baseType);
                    _SysClassCache.Add(element.FullName, baseType);
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
            if (!item.IsRenamingAllowed())
                return true;
            if (item is CRenameItemClass)
                SetNewName(item, _RuleSet.Class);
            else if (item is CRenameItemInterface)
                SetNewName(item, _RuleSet.Interface);
            else if (item is CRenameItemEnum)
                SetNewName(item, _RuleSet.Enum);
            else if (item is CRenameItemStruct)
                SetNewName(item, _RuleSet.Struct);
            else if (item is CRenameItemEvent)
                SetNewName(item, _RuleSet.Event);
            else if (item is CRenameItemLocalVariable)
            {
                if (((CRenameItemLocalVariable)item).IsConst)
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
            TraverseItemContainer(SetNewName);
        }
        #endregion

        #region ApplyChanges
        private static bool ApplyChangesPre(CRenameItem item)
        {
            _WorkStatus.SubValue++;
            if (!(item is CRenameItemLocalVariable))
            {
                if (item.Name != item.NewName)
                {
                    Message("Renaming " + item.Parent.Name + "." + item.Name);
                    item.NewName = _TmpPrefix + item.NewName;
                    item.Rename();
                }
            }
            return true;
        }

        private static bool ApplyChangesLocalVar(CRenameItem item)
        {
            _WorkStatus.SubValue++;
            CRenameItemMethod method = item as CRenameItemMethod;
            if (method != null)
            {
                method.ReloadText();
                foreach (CRenameItemLocalVariable localVar in method.LocalVars)
                    localVar.Rename();
                method.ApplyNewText();
            }
            return true;
        }

        private static bool ApplyChangesPost(CRenameItem item)
        {
            _WorkStatus.SubValue++;
            if (!(item is CRenameItemLocalVariable))
            {
                if (item.Name.StartsWith(_TmpPrefix))
                {
                    item.NewName = item.Name.Substring(_TmpPrefix.Length);
                    item.Rename();
                }
            }
            return true;
        }

        private void ApplyChanges()
        {
            _WorkStatus.SetText("Applying changes");
            _WorkStatus.SubMax = _ElCount * 2;
            if (!TraverseItemContainer(ApplyChangesPre) || !TraverseItemContainer(ApplyChangesLocalVar))
                return;
            BuildClassTree();
            CountElements();
            _WorkStatus.SetText("Applying final changes");
            _WorkStatus.SubMax = _ElCount;
            TraverseItemContainer(ApplyChangesPost);
        }
        #endregion

        private static bool CheckChanges(CRenameItem item)
        {
            _WorkStatus.SubValue++;
            CRenameItem conflictItem = item.GetConflictItem();
            if (conflictItem == null)
                return true;
            string name = item.Name;
            AddParentName(ref name, item);
            Message("Cannot rename " + item.GetTypeName() + " " + name + " to " + item.NewName +
                    " as another identifier with the same name already exists or is about to be renamed to the same name!");
            Conflicts.Add(new ItemTuple(item, conflictItem));
            return true;
        }

        private bool CheckChanges()
        {
            _WorkStatus.SetText("Validating changes");
            return TraverseItemContainer(CheckChanges);
        }

        #region TraverseItems
        private delegate bool HandleItem(CRenameItem item);

        private bool TraverseItemContainer(HandleItem callBack)
        {
            return TraverseItemContainer(_RenameItems, callBack);
        }

        private bool TraverseItemContainer(IRenameItemContainer itemContainer, HandleItem callBack)
        {
            CRenameItemMethod itemMethod = itemContainer as CRenameItemMethod;
            if (itemMethod != null)
            {
                return TraverseItemList(itemMethod.Parameters, callBack) &&
                       TraverseItemList(itemMethod.LocalVars, callBack);
            }
            CRenameItemInterfaceBase itemInterface = (CRenameItemInterfaceBase)itemContainer;
            CRenameItemClass itemClass = itemInterface as CRenameItemClass;
            if (itemClass != null)
            {
                if (!TraverseItemList(itemClass.Classes, callBack) ||
                    !TraverseItemList(itemClass.Interfaces, callBack) ||
                    !TraverseItemList(itemClass.Events, callBack) ||
                    !TraverseItemList(itemClass.Variables, callBack) ||
                    !TraverseItemList(itemClass.Types, callBack))
                    return false;
            }
            return TraverseItemList(itemInterface.Properties, callBack) &&
                   TraverseItemList(itemInterface.Methods, callBack);
        }

        private bool TraverseItemContainer(CRenameItemNamespace itemNamespace, HandleItem callBack)
        {
            return TraverseItemList(itemNamespace.Classes, callBack) &&
                   TraverseItemList(itemNamespace.Interfaces, callBack) &&
                   TraverseItemList(itemNamespace.Types, callBack) &&
                   TraverseItemList(itemNamespace.Namespaces, callBack);
        }

        private bool TraverseItemList<T>(IEnumerable<T> list, HandleItem callBack) where T : CRenameItem
        {
            foreach (T item in list)
            {
                if (!callBack(item))
                    return false;
                CRenameItemNamespace itemNs = item as CRenameItemNamespace;
                if (itemNs != null)
                {
                    if (!TraverseItemContainer(itemNs, callBack))
                        return false;
                }
                else
                {
                    IRenameItemContainer container = item as IRenameItemContainer;
                    if (container != null && !TraverseItemContainer(container, callBack))
                        return false;
                }
            }
            return true;
        }
        #endregion

        public static void Message(string msg)
        {
            _OutputWindow.OutputString(msg + "\r\n");
        }

        private static class CTypeResolver
        {
            private static List<string> _Alias;

            public static void AddTypes()
            {
                if (_Alias != null)
                    return;
                _Alias = new List<string>
                    {
                        "bool",
                        "byte",
                        "sbyte",
                        "char",
                        "decimal",
                        "double",
                        "float",
                        "int",
                        "uint",
                        "long",
                        "ulong",
                        "object",
                        "short",
                        "ushort",
                        "string"
                    };

                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                _WorkStatus.SubMax += assemblies.Length * 251;
                foreach (Assembly a in assemblies)
                {
                    try
                    {
                        Type[] types = a.GetTypes();
                        _WorkStatus.SubMax += types.Length - 250;
                        foreach (Type type in types)
                        {
                            _Alias.Add(type.Name);
                            _WorkStatus.SubValue++;
                        }
                    }
                    catch
                    {
                        _WorkStatus.SubValue += 250;
                    }
                    _WorkStatus.SubValue++;
                }
            }

            public static bool IsType(string typeName)
            {
                if (_Alias == null)
                    AddTypes();
                // ReSharper disable PossibleNullReferenceException
                return _Alias.Contains(typeName);
                // ReSharper restore PossibleNullReferenceException
            }
        }

        // Implementierung der Schnittstelle IDisposable
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!_IsDisposed)
            {
                // Methode wird zum ersten Mal aufgerufen
                if (disposing)
                {
                    _FormConflicts.Dispose();
                    _FormStatus.Dispose();
                    _UpdateTimer.Dispose();
                }
                // Hier unmanaged Objekte freigeben (z.B. IntPtr)
            }
            // Dafür sorgen, dass Methode nicht mehr aufgerufen werden kann.
            _IsDisposed = true;
        }
    }
}