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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace NamingFix
{
    using ItemTuple = Tuple<CRenameItem, CRenameItem>;
    using ItemStringTuple = Tuple<CRenameItem, String>;

    class CNamingFix : IDisposable
    {
        private struct SWorkStatus
        {
            public int MainValue;
            public int SubValue, SubMax;
            public string Text { get; set; }
            public string Exception;

            public void SetText(string text, bool addMain = true)
            {
                Text = text;
                SubValue = 0;
                if (addMain)
                    MainValue++;
            }
        }

        private readonly DTE2 _Dte;
        private static OutputWindowPane _OutputWindow;
        private CRenameRuleSet _RuleSet;
        private static readonly Regex _ReUnderScore = new Regex("(?<=[a-z0-9])_[a-z0-9]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex _ReMultiCaps = new Regex("[A-Z]{2,}", RegexOptions.Compiled);

        private CRenameItemNamespace _RenameItems;
        private readonly Dictionary<string, CRenameItemInterfaceBase> _SysClassCache = new Dictionary<string, CRenameItemInterfaceBase>();

        public static readonly List<String> FoundTypes = new List<string>();

        private readonly CFormStatus _FormStatus = new CFormStatus();
        private static readonly CFormConflicts _FormConflicts = new CFormConflicts();
        private static readonly CFormRemarks _FormRemarks = new CFormRemarks();
        public static readonly List<ItemTuple> Conflicts = new List<ItemTuple>();
        public static readonly List<ItemStringTuple> Remarks = new List<ItemStringTuple>();
        private List<string> _ReservedWords;
        private static int _ElCount;
        private const string _TmpPrefix = "RNFTMPPRE";
        private const int _MainStepCount = 8;
        private const int _MainStepCountAnalyze = 6;
        private System.Threading.Thread _WorkerThread;
        private static SWorkStatus _WorkStatus;
        private readonly Timer _UpdateTimer = new Timer();
        private bool _IsDisposed;
        public static bool IsAbort;
        private int _ProjectItemCount;
        private bool _IsAnalyzeOnly;

        #region Init
        public CNamingFix(DTE2 dte)
        {
            _Dte = dte;
            _OutputWindow = _GetPane("Naming Fix AddIn");
            _OutputWindow.Activate();
            _UpdateTimer.Interval = 120;
            _UpdateTimer.Tick += Timer_ShowStatus;
            _InitReservedWords();
        }

        private OutputWindowPane _GetPane(string name)
        {
            Window window = _Dte.Windows.Item(Constants.vsWindowKindOutput);
            OutputWindow outputWindow = (OutputWindow)window.Object;
            OutputWindowPane oPane = outputWindow.OutputWindowPanes.Cast<OutputWindowPane>().FirstOrDefault(pane => pane.Name == name);
            return oPane ?? outputWindow.OutputWindowPanes.Add(name);
        }

        private void _InitReservedWords()
        {
            _ReservedWords = new List<string>
                {
                    "abstract",
                    "as",
                    "base",
                    "bool",
                    "break",
                    "byte",
                    "case",
                    "catch",
                    "char",
                    "checked",
                    "class",
                    "const",
                    "continue",
                    "decimal",
                    "default",
                    "delegate",
                    "do",
                    "double",
                    "else",
                    "enum",
                    "event",
                    "explicit",
                    "extern",
                    "false",
                    "finally",
                    "fixed",
                    "float",
                    "for",
                    "foreach",
                    "goto",
                    "if",
                    "implicit",
                    "in",
                    "int",
                    "interface",
                    "internal",
                    "is",
                    "lock",
                    "long",
                    "namespace",
                    "new",
                    "null",
                    "object",
                    "operator",
                    "out",
                    "override",
                    "params",
                    "private",
                    "protected",
                    "public",
                    "readonly",
                    "ref",
                    "return",
                    "sbyte",
                    "sealed",
                    "short",
                    "sizeof",
                    "stackalloc",
                    "static",
                    "string",
                    "struct",
                    "switch",
                    "this",
                    "throw",
                    "true",
                    "try",
                    "typeof",
                    "uint",
                    "ulong",
                    "unchecked",
                    "unsafe",
                    "ushort",
                    "using",
                    "virtual",
                    "void",
                    "volatile",
                    "while"
                };
        }
        #endregion

        /// <summary>
        ///     This function is used to execute a command when the a menu item is clicked.
        /// </summary>
        public void DoFix(bool analyzeOnly)
        {
            if (_WorkerThread != null)
                return;
            _OutputWindow.Clear();
            _FormConflicts.Hide();
            _FormRemarks.Hide();
            if (MessageBox.Show(
                "To avoid possible side effects please make sure your solution compiles completely without any errors at all!\r\n\r\nAlso make sure you have a backup or current commit.\r\n\r\nContinue?",
                "Naming Fix", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;
            _IsAnalyzeOnly = analyzeOnly;
            if (analyzeOnly)
            {
                _FormStatus.lblAction.Text = "Analyzing namings. Check Outputpanel for remarks!";
                _FormStatus.pbMain.Maximum = _MainStepCountAnalyze;
            }
            else
            {
                _FormStatus.lblAction.Text = "Applying given name sheme";
                _FormStatus.pbMain.Maximum = _MainStepCount;
                _Dte.UndoContext.Open("Item renaming");
            }
            _WorkStatus.MainValue = 0;
            _WorkStatus.SetText("Initializing", false);
            _WorkStatus.Exception = "";
            _ShowStatus();
            _FormStatus.Show();
            IsAbort = false;
            _WorkerThread = new System.Threading.Thread(_Execute);
            _WorkerThread.Start();
            _UpdateTimer.Start();
        }

        private void Timer_ShowStatus(object sender, EventArgs e)
        {
            if (!_WorkerThread.IsAlive)
            {
                if (!_IsAnalyzeOnly)
                    _Dte.UndoContext.Close();
                _FormStatus.Hide();
                _UpdateTimer.Stop();
                _WorkerThread = null;
                if (_WorkStatus.Exception != "")
                    MessageBox.Show(_WorkStatus.Exception, "Exception occured", MessageBoxButtons.OK, MessageBoxIcon.Error);
                else
                {
                    _ShowConflicts();
                    _ShowRemarks();
                    _FormRemarks.Left = Screen.PrimaryScreen.WorkingArea.Width - _FormRemarks.Width;
                    _FormRemarks.Top = 0;
                    _FormConflicts.Left = Screen.PrimaryScreen.WorkingArea.Width - _FormConflicts.Width;
                    _FormConflicts.Top = _FormRemarks.Top + _FormRemarks.Height;
                    if (_FormConflicts.Top > Screen.PrimaryScreen.WorkingArea.Height - 200)
                        _FormConflicts.Top = Screen.PrimaryScreen.WorkingArea.Height - 200;
                }
            }
            else
            {
                if (IsAbort)
                    _WorkerThread.Abort();
                _ShowStatus();
            }
        }

        private void _ShowStatus()
        {
            _FormStatus.pbMain.Value = _WorkStatus.MainValue;
            int value = _WorkStatus.SubValue;
            _FormStatus.pbSub.Maximum = _WorkStatus.SubMax;
            if (value <= _FormStatus.pbSub.Maximum)
                _FormStatus.pbSub.Value = value;
            _FormStatus.lblText.Text = _WorkStatus.Text;
        }

        private void _Execute()
        {
            Message("Analysis started!");
            try
            {
                CTypeResolver.AddTypes();
                FoundTypes.Clear();
                Conflicts.Clear();
                Remarks.Clear();
                if (!_BuildClassTree())
                    return;
                _CountElements();
                _GetInheritanceInfo();
                _RuleSet = new CRenameRuleSet();
                _SetNewNames();
                Message("Analysis finished!");
                if (_CheckChanges() && !_IsAnalyzeOnly && Conflicts.Count == 0)
                    _ApplyChanges();

                Message("\r\nFound local var types:");
                foreach (string type in FoundTypes.Where(type => !CTypeResolver.IsType(type)))
                    Message(type);
                Message("IMPORTANT: Check if all found types above are actual types otherwhise use undo and fix addin!");
            }
            catch (Exception exception)
            {
                _WorkStatus.Exception = "Exception occured!:\r\n" + exception.Message;
                if (!_IsAnalyzeOnly)
                    _WorkStatus.Exception += "\r\n\r\nIf there have been any changes already applied, revert the whole solution or try to use undo!";
                Message("Exception Stacktrace: " + exception);
            }
            Message("Processing finished!");
        }

        private static void _ShowConflicts()
        {
            if (Conflicts.Count == 0)
                return;
            _FormConflicts.lbConflicts.Items.Clear();
            foreach (ItemTuple item in Conflicts)
            {
                string name = item.Item1.Name;
                CUtils.AddParentName(ref name, item.Item1);
                string description;
                if (item.Item2 != null)
                {
                    string name2 = item.Item2.Name;
                    if (item.Item1.Parent.Name != item.Item2.Parent.Name)
                        CUtils.AddParentName(ref name2, item.Item2);
                    description = " <=> " + item.Item2.GetTypeName() + " " + name2;
                }
                else
                    description = "(reserved word)";
                _FormConflicts.lbConflicts.Items.Add(item.Item1.GetTypeName() + " " + name + " --> " + item.Item1.NewName + description);
            }
            _FormConflicts.Show();
        }

        private static void _ShowRemarks()
        {
            if (Remarks.Count == 0)
                return;
            Remarks.Sort((item1, item2) => String.Compare(item1.Item2, item2.Item2, StringComparison.Ordinal));
            _FormRemarks.lbRemarks.Items.Clear();
            foreach (ItemStringTuple item in Remarks)
                _FormRemarks.lbRemarks.Items.Add(item.Item2);
            _FormRemarks.Show();
        }

        #region BuildClassTree
        private static void _AddProjects(IEnumerable<Project> projectsIn, List<Project> projectsOut, BuildDependencies dependencies, ref int itemCount)
        {
            foreach (Project project in projectsIn)
            {
                if (projectsOut.Any(item => item.UniqueName == project.UniqueName))
                    continue;
                BuildDependency dependency = dependencies.Item(project);
                if (dependency != null)
                    _AddProjects(((Array)dependency.RequiredProjects).Cast<Project>(), projectsOut, dependencies, ref itemCount);
                itemCount += project.ProjectItems.Count;
                projectsOut.Add(project);
                _WorkStatus.SubValue++;
            }
        }

        private bool _BuildClassTree()
        {
            _WorkStatus.SetText("Analysing project dependecies");
            _RenameItems = new CRenameItemNamespace();
            _SysClassCache.Clear();
            _WorkStatus.SubMax = _Dte.Solution.Projects.Count + 1;
            if (_Dte.Solution.Projects.Count == 0)
            {
                _WorkStatus.Exception = "No Projects to process. Please open one before applying this!";
                return false;
            }
            List<Project> projects = new List<Project>(_Dte.Solution.Projects.Count);
            BuildDependencies dependencies = _Dte.Solution.SolutionBuild.BuildDependencies;
            _AddProjects(_Dte.Solution.Projects.Cast<Project>(), projects, dependencies, ref _ProjectItemCount);
            _WorkStatus.SetText("Gathering classes");
            _WorkStatus.SubMax = _ProjectItemCount + projects.Count;
            foreach (Project project in projects)
            {
                _IterateProjectItems(project.ProjectItems, _ProcessCodeElementsInProjectItem);
                _WorkStatus.SubValue++;
            }
            return true;
        }

        private delegate void HandleProjectItem(ProjectItem item);

        private static void _IterateProjectItems(ProjectItems projectItems, HandleProjectItem callback)
        {
            foreach (ProjectItem item in projectItems)
            {
                if (item.Kind == Constants.vsProjectItemKindPhysicalFile && item.Name.EndsWith(".cs") &&
                    item.FileCodeModel.Language == CodeModelLanguageConstants.vsCMLanguageCSharp)
                {
#if(DEBUG)
                    Message("File " + item.Name + ":");
#endif
                    callback(item);
                }
                ProjectItems subItems = item.SubProject != null ? item.SubProject.ProjectItems : item.ProjectItems;
                if (subItems != null && subItems.Count > 0)
                {
                    _WorkStatus.SubMax += subItems.Count;
                    _IterateProjectItems(subItems, callback);
                }
                _WorkStatus.SubValue++;
            }
        }

        private void _ProcessCodeElementsInProjectItem(ProjectItem item)
        {
            _IterateCodeElements(item.FileCodeModel.CodeElements, _RenameItems);
        }

        //Iterate through all the code elements in the provided element
        private static void _IterateCodeElements(CodeElements colCodeElements, IRenameItemContainer curParent)
        {
            //Check for nonmutable object inheritance
            if (colCodeElements == null)
                return;
            foreach (CodeElement element in colCodeElements)
            {
                try
                {
                    CRenameItemElement cItem = null;
                    CodeElements subElements = null;
                    switch (element.Kind)
                    {
                        case vsCMElement.vsCMElementVariable:
                            if (curParent is CRenameItemEnum)
                                cItem = new CRenameItemEnumMember();
                            else
                                cItem = new CRenameItemVariable();
                            break;
                        case vsCMElement.vsCMElementProperty:
                            cItem = new CRenameItemProperty();
                            break;
                        case vsCMElement.vsCMElementFunction:
                            cItem = new CRenameItemMethod();
                            subElements = ((CodeFunction2)element).Parameters;
                            break;
                        case vsCMElement.vsCMElementParameter:
                            cItem = new CRenameItemParameter();
                            break;
                        case vsCMElement.vsCMElementInterface:
                            cItem = new CRenameItemInterface();
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
                            _IterateCodeElements(objCodeNamespace.Members, ns);
                            break;
                        case vsCMElement.vsCMElementClass:
                            cItem = new CRenameItemClass();
                            break;
                        case vsCMElement.vsCMElementDelegate:
                            cItem = new CRenameItemDelegate();
                            subElements = ((CodeDelegate2)element).Parameters;
                            break;
                        case vsCMElement.vsCMElementEvent:
                            cItem = new CRenameItemEvent();
                            break;
                        case vsCMElement.vsCMElementImportStmt:
                        case vsCMElement.vsCMElementAttribute:
                            break;
                        default:
                            Message("Unhandled element kind: " + element.Kind);
                            break;
                    }
                    if (cItem == null)
                        continue;
                    cItem.IsSystem = curParent.IsSystem;
                    cItem.Element = element;
                    cItem.Name = element.Name;
                    curParent.Add(cItem);
                    if (subElements == null)
                    {
                        CodeType type = element as CodeType;
                        if (type != null)
                            subElements = type.Members;
                    }
                    IRenameItemContainer newParent = cItem as IRenameItemContainer;
                    if (subElements != null && newParent != null)
                        _IterateCodeElements(subElements, newParent);
                }
                catch {}
            }
        }

        private static bool _CountElems(CRenameItem item)
        {
            _ElCount++;
            return true;
        }

        private void _CountElements()
        {
            _WorkStatus.SetText("Initializing and counting elements");
            _ElCount = 0;
            _TraverseItemContainer(_CountElems);
        }

        private void _GetInheritanceInfo()
        {
            _WorkStatus.SetText("Gathering inheritance info");
            _WorkStatus.SubMax = 21;
            _AddInheritedInfoToMembers(_RenameItems);
        }

        private void _AddInheritedInfoToMembers(CRenameItemClass child)
        {
            _WorkStatus.SubMax += child.Classes.Count * 6 + child.Interfaces.Count - 5;
            foreach (CRenameItemClass item in child.Classes)
            {
                _GetInherited(item);
                _WorkStatus.SubValue++;
            }
            foreach (CRenameItemInterface item in child.Interfaces)
            {
                _GetInherited(item);
                _WorkStatus.SubValue++;
            }
        }

        private void _AddInheritedInfoToMembers(CRenameItemNamespace child)
        {
            _WorkStatus.SubMax += child.Classes.Count * 6 + child.Interfaces.Count + child.Namespaces.Count * 21 - 20;
            foreach (CRenameItemClass item in child.Classes)
            {
                _GetInherited(item);
                _WorkStatus.SubValue++;
            }
            foreach (CRenameItemInterface item in child.Interfaces)
            {
                _GetInherited(item);
                _WorkStatus.SubValue++;
            }
            foreach (CRenameItemNamespace item in child.Namespaces)
            {
                _AddInheritedInfoToMembers(item);
                _WorkStatus.SubValue++;
            }
        }

        private void _GetInherited(CRenameItemClass child)
        {
            if (child.IsInheritedLoaded)
                return;
#if(DEBUG)
            Message("Get inherited: " + child.Name);
#endif
            foreach (CodeElement baseClass in child.GetElement().Bases)
                _ProcessBaseElement<CRenameItemClass>(baseClass, child);
            foreach (CodeElement implInterface in child.GetElement().ImplementedInterfaces)
                _ProcessBaseElement<CRenameItemInterface>(implInterface, child);
            child.IsInheritedLoaded = true;
            _AddInheritedInfoToMembers(child);
        }

        private void _GetInherited(CRenameItemInterface child)
        {
            if (child.IsInheritedLoaded)
                return;
#if(DEBUG)
            Message("Get inherited: " + child.Name);
#endif
            foreach (CodeElement element in child.GetElement().Bases)
                _ProcessBaseElement<CRenameItemInterface>(element, child);
            child.IsInheritedLoaded = true;
        }

        private void _ProcessBaseElement<T>(CodeElement element, CRenameItemInterfaceBase child) where T : CRenameItemInterfaceBase, new()
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
                    baseType = new T {IsSystem = true, Element = element, Name = element.Name};
                    _IterateCodeElements(((CodeType)element).Members, baseType);
                    _SysClassCache.Add(element.FullName, baseType);
                }
            }

            //set derivedinfo
            baseType.CopyIdsDerived(child);

            //Make sure, inheritance info is filled
            CRenameItemClass tmpClass = baseType as CRenameItemClass;
            if (tmpClass != null)
                _GetInherited(tmpClass);
            else
                _GetInherited(baseType as CRenameItemInterface);
            //Get all Ids from class and its parents
            child.CopyIds(baseType);
        }
        #endregion

        #region GetNewName
        private string _GetNewName(CRenameItem item, SRenameRule rule)
        {
            String theString = item.Name;
            if (!String.IsNullOrEmpty(rule.DontChangePrefix) && theString.StartsWith(rule.DontChangePrefix))
                return theString;

            //Handle fixed names
            if (_RuleSet.FixedNames.Contains(theString))
                return theString;

            //Remove prefix if it is already there
            if (_RemovePrefix(ref theString, rule.Prefix))
            {
                //Handle fixed names
                if (_RuleSet.FixedNames.Contains(theString))
                    return rule.Prefix + theString;
            }

            //Check for non-letter prefixes
            if (theString.Length > 1 && _RuleSet.IdStartsWithLetter && !Char.IsLetter(theString, 0))
            {
                if (_RuleSet.RemoveNonLetterPrefixNoReport.IndexOf(theString[0]) < 0)
                    Remarks.Add(new ItemStringTuple(item, "Stripping non-letter prefix: " + theString));
                theString = theString.Substring(1);
            }

            //Remove prefixes like "type-prefixes" (iNum -> Num, otherwhise could get Inum)
            if (theString.Length > 1 && Char.IsLower(theString, 0) && Char.IsUpper(theString, 1))
            {
                string prefixStripped = theString.Substring(1);
                string abbrev = _RuleSet.Abbreviations.FirstOrDefault(ab => theString.StartsWith(ab));
                string checkString = (abbrev == null) ? prefixStripped : prefixStripped.Remove(0, abbrev.Length);
                bool isUpper = checkString.Length == 0 || !checkString.Any(Char.IsLower);
                if (!isUpper)
                {
                    Remarks.Add(new ItemStringTuple(item, "Stripping prefix: " + theString + " -> " + prefixStripped));
                    theString = prefixStripped;
                }
            }

            switch (rule.NamingStyle)
            {
                case
                    ENamingStyle.LowerCamelCase:
                    if (rule.Prefix != null && Char.IsLower(rule.Prefix, rule.Prefix.Length - 1))
                        _ToUpperCamelCase(ref theString, item);
                    else
                        _ToLowerCamelCase(ref theString, item);
                    break;
                case ENamingStyle.UpperCamelCase:
                    _ToUpperCamelCase(ref theString, item);
                    break;
                case ENamingStyle.UpperCase:
                    theString = theString.ToUpper();
                    break;
                case ENamingStyle.LowerCase:
                    theString = theString.ToLower();
                    break;
            }
            _AddPrefix(ref theString, rule.Prefix);

            return theString;
        }

        private static bool _RemovePrefix(ref string theString, string prefix)
        {
            if (string.IsNullOrEmpty(prefix))
                return false;
            if (theString.Length <= prefix.Length || !theString.StartsWith(prefix))
                return false;
            //Do not remove if prexix ends with a capital and next char is no capital
            //So remove if prefix ends with anything but a capital or next char in string is a capital
            //In that case it is likely that the prefix is part of the name. e.g. prefix "C" and name "class CodeBook"
            if (!Char.IsUpper(prefix, prefix.Length - 1) || Char.IsUpper(theString, prefix.Length))
            {
                theString = theString.Remove(0, prefix.Length);
                return true;
            }
            return false;
        }

        private void _ToLowerCamelCase(ref String theString, CRenameItem item)
        {
            _ToCamelCase(ref theString, item);
            theString = theString.Substring(0, 1).ToLower() + theString.Substring(1);
        }

        private void _ToUpperCamelCase(ref String theString, CRenameItem item)
        {
            theString = theString.Substring(0, 1).ToUpper() + theString.Substring(1);
            _ToCamelCase(ref theString, item);
        }

        private void _ToCamelCase(ref String theString, CRenameItem item)
        {
            theString = theString.Replace("__", "_");
            string input = theString;
            theString = _ReMultiCaps.Replace(theString, m =>
                {
                    string abbrev = _RuleSet.Abbreviations.FirstOrDefault(abb => m.Value.StartsWith(abb));
                    if (abbrev != null)
                    {
                        //More uppercase chars then in abbrev --> allow only 1 more
                        if (m.Value.Length > abbrev.Length + 1)
                            return m.Value.Substring(0, abbrev.Length) + m.Value.Substring(abbrev.Length).ToLower();
                        return m.Value;
                    }
                    //Allow partial abbrevs like MP3 (would only match "MP")
                    abbrev = _RuleSet.PartialAbbreviations.FirstOrDefault(abb => abb.StartsWith(m.Value));
                    if (abbrev != null)
                    {
                        if (input.Substring(m.Index).StartsWith(abbrev))
                            return m.Value;
                    }
                    Remarks.Add(new ItemStringTuple(item, "Possible abbreviation: " + m.Value + " in " + input));
                    return m.Value[0] + m.Value.Substring(1).ToLower();
                });
            theString = _ReUnderScore.Replace(theString, m => m.Value[1].ToString().ToUpper());
        }

        private static void _AddPrefix(ref String theString, string prefix = "")
        {
            if (String.IsNullOrEmpty(prefix))
                return;
            theString = prefix + theString;
        }
        #endregion

        #region SetNewNames
        private void _SetNewName(CRenameItem item, SRenameRule rule)
        {
            item.NewName = _GetNewName(item, rule);
        }

        private void _SetNewName(CRenameItem item, SRenameRule[] rules, vsCMAccess access)
        {
            switch (access)
            {
                case vsCMAccess.vsCMAccessPrivate:
                    _SetNewName(item, rules[CRenameRuleSet.Priv]);
                    break;
                case vsCMAccess.vsCMAccessProtected:
                    _SetNewName(item, rules[CRenameRuleSet.Prot]);
                    break;
                case vsCMAccess.vsCMAccessPublic:
                case vsCMAccess.vsCMAccessProject:
                    _SetNewName(item, rules[CRenameRuleSet.Pub]);
                    break;
                default:
                    Message("Found unknown access modifier for " + item.Name + " " + access);
                    break;
            }
        }

        private void _SetNewName(CRenameItemMethod func)
        {
            _SetNewName(func, _RuleSet.Method, func.Access);
        }

        private void _SetNewName(CRenameItemVariable variable)
        {
            SRenameRule[] rules = (variable.GetElement().IsConstant) ? _RuleSet.Const : _RuleSet.Field;
            _SetNewName(variable, rules, variable.GetElement().Access);
        }

        private void _SetNewName(CRenameItemProperty property)
        {
            _SetNewName(property, _RuleSet.Property, property.GetElement().Access);
        }

        private bool _SetNewName(CRenameItem item)
        {
            // ReSharper disable CanBeReplacedWithTryCastAndCheckForNull
            _WorkStatus.SubValue++;
            if (!item.IsRenamingAllowed())
                return true;
            if (item is CRenameItemClass)
                _SetNewName(item, _RuleSet.Class);
            else if (item is CRenameItemInterface)
                _SetNewName(item, _RuleSet.Interface);
            else if (item is CRenameItemEnum)
                _SetNewName(item, _RuleSet.Enum);
            else if (item is CRenameItemStruct)
                _SetNewName(item, _RuleSet.Struct);
            else if (item is CRenameItemEvent)
                _SetNewName(item, _RuleSet.Event);
            else if (item is CRenameItemDelegate)
                _SetNewName(item, _RuleSet.Delegate);
            else if (item is CRenameItemLocalVariable)
            {
                if (((CRenameItemLocalVariable)item).IsConst)
                    _SetNewName(item, _RuleSet.LokalConst);
                else
                    _SetNewName(item, _RuleSet.LokalVariable);
            }
            else if (item is CRenameItemVariable)
                _SetNewName((CRenameItemVariable)item);
            else if (item is CRenameItemProperty)
                _SetNewName((CRenameItemProperty)item);
            else if (item is CRenameItemEnumMember)
                _SetNewName(item, _RuleSet.EnumMember);
            else if (item is CRenameItemParameter)
                _SetNewName(item, _RuleSet.Parameter);
            else if (item is CRenameItemMethod)
                _SetNewName((CRenameItemMethod)item);
            return true;
            // ReSharper restore CanBeReplacedWithTryCastAndCheckForNull
        }

        private void _SetNewNames()
        {
            _WorkStatus.SetText("Calculating new names");
            _WorkStatus.SubMax = _ElCount;
            _TraverseItemContainer(_SetNewName);
        }
        #endregion

        #region ApplyChanges
        private static void _ShowNotRenamed(CRenameItem item)
        {
            string name = item.Name;
            if (item.Parent.Name != "")
                name = item.Parent.Name + "." + name;
            if (((CRenameItem)item.Parent).Parent != null && ((CRenameItem)item.Parent).Parent.Name != "")
                name = ((CRenameItem)item.Parent).Parent.Name + "." + name;
            Message("Did not rename " + name);
        }

        private static bool _ApplyChangesPre(CRenameItem item)
        {
            _WorkStatus.SubValue++;
            if (!(item is CRenameItemLocalVariable))
            {
                if (item.Name != item.NewName)
                {
                    CRenameItemElement itemElement = item as CRenameItemElement;
                    if (itemElement != null)
                        itemElement.RefreshElement();
                    _WorkStatus.Text = "Applying changes: " + item.Name;
                    if (item.GetConflictItem(true) != null)
                        item.NewName = _TmpPrefix + item.NewName;
                    if (!item.Rename())
                        _ShowNotRenamed(item);
                }
            }
            return true;
        }

        private static bool _ApplyChangesLocalVar(CRenameItem item)
        {
            _WorkStatus.SubValue++;
            CRenameItemMethod method = item as CRenameItemMethod;
            if (method != null)
            {
                if (method.LocalVars.All(localVar => localVar.Name == localVar.NewName))
                    return true;
                _WorkStatus.Text = "Applying local variable names: " + method.Name;
                method.RefreshElement();
                method.ReloadText();
                //Avoid conflicts by using temporary names
                foreach (CRenameItemLocalVariable localVar in method.LocalVars.Where(localVar => localVar.Name != localVar.NewName))
                {
                    if (method.LocalVars.Any(localVariable => localVariable.Name == localVar.NewName))
                        localVar.NewName = _TmpPrefix + localVar.NewName;
                    localVar.Rename();
                }
                foreach (CRenameItemLocalVariable localVar in method.LocalVars.Where(localVar => localVar.Name.StartsWith(_TmpPrefix)))
                {
                    localVar.NewName = localVar.Name.Substring(_TmpPrefix.Length);
                    localVar.Rename();
                }
                method.ApplyNewText();
            }
            return true;
        }

        private static bool _ApplyChangesPost(CRenameItem item)
        {
            _WorkStatus.SubValue++;
            if (!(item is CRenameItemLocalVariable))
            {
                if (item.Name.StartsWith(_TmpPrefix))
                {
                    CRenameItemElement itemElement = item as CRenameItemElement;
                    if (itemElement != null)
                    {
                        itemElement.RefreshElement();
                        if (!item.Name.StartsWith(_TmpPrefix))
                            return true;
                    }
                    item.NewName = item.Name.Substring(_TmpPrefix.Length);
                    item.Rename();
                }
            }
            return true;
        }

        private void _ApplyChanges()
        {
            _WorkStatus.SetText("Applying changes");
            _WorkStatus.SubMax = _ElCount * 2;
            if (!_TraverseItemContainer(_ApplyChangesPre))
                return;
            if (!_TraverseItemContainer(_ApplyChangesLocalVar))
                return;
            _WorkStatus.SetText("Applying final changes");
            _WorkStatus.SubMax = _ElCount;
            _TraverseItemContainer(_ApplyChangesPost);
        }
        #endregion

        private bool _CheckChanges(CRenameItem item)
        {
            _WorkStatus.SubValue++;
            if (item.Name == item.NewName)
                return true;
            CRenameItem conflictItem = null;
            String msg;
            if (_ReservedWords.Contains(item.NewName))
                msg = " as this is a reserved name!";
            else
            {
                conflictItem = item.GetConflictItem(false);
                if (conflictItem == null)
                    return true;
                msg = " as another identifier with the same name already exists or is about to be renamed to the same name!";
            }
            string name = item.Name;
            CUtils.AddParentName(ref name, item);
            Conflicts.Add(new ItemTuple(item, conflictItem));
            Message("Cannot rename " + item.GetTypeName() + " " + name + " to " + item.NewName + msg);
            return true;
        }

        private bool _CheckChanges()
        {
            _WorkStatus.SetText("Validating changes");
            return _TraverseItemContainer(_CheckChanges);
        }

        #region TraverseItems
        private delegate bool HandleItem(CRenameItem item);

        private bool _TraverseItemContainer(HandleItem callBack)
        {
            return _TraverseItemContainer(_RenameItems, callBack);
        }

        private bool _TraverseItemContainer(IRenameItemContainer itemContainer, HandleItem callBack)
        {
            return itemContainer.Cast<IEnumerable>().All(renameItems => _TraverseItemList(renameItems, callBack));
        }

        private bool _TraverseItemList(IEnumerable list, HandleItem callBack)
        {
            foreach (CRenameItem item in list)
            {
                if (!callBack(item))
                    return false;
                CRenameItemNamespace itemNs = item as CRenameItemNamespace;
                if (itemNs != null)
                {
                    if (!_TraverseItemContainer(itemNs, callBack))
                        return false;
                }
                else
                {
                    IRenameItemContainer container = item as IRenameItemContainer;
                    if (container != null && !_TraverseItemContainer(container, callBack))
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
            _Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void _Dispose(bool disposing)
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