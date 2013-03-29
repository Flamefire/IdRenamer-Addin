#region license
/*
    This file is part of the item renamer Add-In for VS ("Add-In").

    The Add-In is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    The Add-In is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with Foobar.  If not, see <http://www.gnu.org/licenses/>.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EnvDTE;
using EnvDTE80;

namespace Variable_Renamer
{
    enum ENamingStyle
    {
        UpperCamelCase,
        LowerCamelCase,
        UpperCase,
        LowerCase
    }

    struct SRenameRule
    {
        public bool DontChange;
        public String RemovePrefix;
        public String Prefix;
        public ENamingStyle NamingStyle;
    }

    class CRenameItem
    {
        public string Name, NewName;
        public CodeElement2 Element;
        public CRenameItemClass Parent;
    }

    class CRenameItemSub : CRenameItem {}

    class CRenameFunction : CRenameItem
    {
        public readonly List<CRenameItem> Parameters = new List<CRenameItem>();
        public string Text = null;
        private EditPoint _StartPt;
        private TextPoint _EndPt;
        private readonly List<string> _Strings = new List<string>();
        private readonly List<string> _Comments = new List<string>();
        private static readonly Regex _ReString = new Regex(@"""(\\.|[^\\""])*""", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex _ReVarbatimString = new Regex("@\"(\"\"|[^\"])*\"", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex _ReCommentSl = new Regex(@"//[^\r\n]*\r\n", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex _ReCommentMl = new Regex(@"/\*.*?\*/", RegexOptions.Compiled | RegexOptions.Multiline);

        public String GetText()
        {
            CodeFunction2 func = (CodeFunction2)Element;
            _StartPt = func.GetStartPoint(vsCMPart.vsCMPartBody).CreateEditPoint();
            _EndPt = func.GetEndPoint(vsCMPart.vsCMPartBody);
            return _StartPt.GetText(_EndPt);
        }

        public void SetText(String text)
        {
            _StartPt.ReplaceText(_EndPt, text, (int)vsEPReplaceTextOptions.vsEPReplaceTextKeepMarkers);
        }

        public void RemoveTextComments(ref String text)
        {
            _Strings.Clear();
            _Comments.Clear();
            text = _ReString.Replace(text, delegate(Match m)
                {
                    _Strings.Add(m.Value);
                    return "\"ReplacedStr:::" + (_Strings.Count - 1) + ":::\"";
                });
            text = _ReVarbatimString.Replace(text, delegate(Match m)
                {
                    _Strings.Add(m.Value);
                    return "\"ReplacedStr:::" + (_Strings.Count - 1) + ":::\"";
                });
            text = _ReCommentSl.Replace(text, delegate(Match m)
                {
                    _Comments.Add(m.Value);
                    return "//ReplacedCom:::" + (_Comments.Count - 1) + ":::;\r\n";
                });
            text = _ReCommentMl.Replace(text, delegate(Match m)
                {
                    _Comments.Add(m.Value);
                    return "//ReplacedCom:::" + (_Comments.Count - 1) + ":::;\r\n";
                });
        }

        public void RestoreTextComments(ref String text)
        {
            for (int i = 0; i < _Strings.Count; i++)
                text = text.Replace("\"ReplacedStr:::" + i + ":::\"", _Strings[i]);
            for (int i = 0; i < _Comments.Count; i++)
                text = text.Replace("//ReplacedCom:::" + i + ":::;\r\n", _Comments[i]);
        }
    }

    class CRenameItemClassBase : CRenameItem
    {
        public readonly List<CRenameFunction> Functions = new List<CRenameFunction>();
        public readonly List<CRenameItem> Variables = new List<CRenameItem>();
        public readonly List<CRenameItem> Properties = new List<CRenameItem>();
        public readonly List<CRenameItemClass> Classes = new List<CRenameItemClass>();
        public readonly List<CRenameItemClass> Interfaces = new List<CRenameItemClass>();
        public readonly List<CRenameItemSub> Structs = new List<CRenameItemSub>();
        public readonly List<CRenameItemSub> Enums = new List<CRenameItemSub>();

        public bool MemberExistsInt(string newName, string oldName = "")
        {
            return Functions.Any(item => item.NewName == newName && item.Name != oldName) ||
                   Variables.Any(item => item.NewName == newName && item.Name != oldName) ||
                   Properties.Any(item => item.NewName == newName && item.Name != oldName);
        }

        public bool IdExistsInt(string newName, string oldName = "")
        {
            return MemberExistsInt(newName, oldName) || IdExistsInt2(newName, oldName);
        }

        private bool IdExistsInt2(string newName, string oldName = "")
        {
            return Classes.Any(item => item.NewName == newName && item.Name != oldName) ||
                   Interfaces.Any(item => item.NewName == newName && item.Name != oldName) ||
                   Structs.Any(item => item.NewName == newName && item.Name != oldName) ||
                   Enums.Any(item => item.NewName == newName && item.Name != oldName) ||
                   (Parent != null && Parent.IdExistsInt2(newName, oldName));
        }
    }

    class CRenameItemClass : CRenameItemClassBase
    {
        public readonly CRenameItemClassBase InheritedStuff;

        public CRenameItemClass()
        {
            InheritedStuff = new CRenameItemClassBase {Parent = this};
        }

        public bool MemberExists(string newName, string oldName = "")
        {
            return MemberExistsInt(newName, oldName) || InheritedStuff.MemberExistsInt(newName, oldName);
        }

        public bool IdExists(string newName, string oldName = "")
        {
            return InheritedStuff.IdExistsInt(newName, oldName);
        }
    }

    class CRenameRuleSet
    {
        public const int Priv = 0;
        public const int Prot = 1;
        public const int Pub = 2;

        public SRenameRule
            Parameter,
            LokalVariable,
            LokalConst,
            Interface,
            Class,
            Enum,
            Struct;
        public SRenameRule[]
            Const,
            Field,
            Property,
            Method;

        public CRenameRuleSet()
        {
            Const = new SRenameRule[3];
            Field = new SRenameRule[3];
            Property = new SRenameRule[3];
            Method = new SRenameRule[3];
            Parameter.NamingStyle = ENamingStyle.LowerCamelCase;
            LokalVariable.NamingStyle = ENamingStyle.LowerCamelCase;
            LokalConst.NamingStyle = ENamingStyle.LowerCamelCase;
            Const[Priv].Prefix = "_";
            Field[Priv].Prefix = "_";
            Method[Priv].Prefix = "_";
            Property[Priv].Prefix = "_";
            Interface.Prefix = "I";
            Class.Prefix = "C";
            Enum.Prefix = "E";
            Struct.Prefix = "S";
        }
    }

    class CItemRenamer
    {
        private const int Priv = 0;
        private const int Prot = 1;
        private const int Pub = 2;
        private readonly DTE2 _Dte;
        private readonly OutputWindowPane _OutputWindow;
        private CRenameRuleSet _RuleSet;
        private const string _Id = "[a-z_][a-z0-9_\\.]*";
        private static readonly Regex _ReLocVar = new Regex(@"(?<=[\{\(,;]\s*)((const\s+)?" + _Id + "(<" + _Id + @">)?)\s*(\[\])?\s+(" + _Id + @")(?=\s*([=,;]|( in )))",
                                                            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private static readonly Regex _ReUnderScore = new Regex("(?<=[a-z0-9])_[a-z0-9]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex _ReMultiCaps = new Regex("[A-Z]{2,}", RegexOptions.Compiled);
        private static readonly Regex _ReEndsWithNoCaps = new Regex("[a-z]$", RegexOptions.Compiled);

        private CRenameItemClass _RenameItems;
        private CRenameItemClassBase _CurParent;

        private readonly List<String> _FoundTypes = new List<string>();

        private readonly CTypeResolver _TypeResolver = new CTypeResolver();
        //Strings
        //comments
        public CItemRenamer(DTE2 dte)
        {
            _Dte = dte;
            Window window = _Dte.Windows.Item(Constants.vsWindowKindOutput);
            OutputWindow outputWindow = (OutputWindow)window.Object;
            _OutputWindow = outputWindow.OutputWindowPanes.Add("new pane");
        }

        /// <summary>
        ///     This function is the callback used to execute a command when the a menu item is clicked.
        ///     See the Initialize method to see how the menu item is associated to this function using
        ///     the OleMenuCommandService service and the MenuCommand class.
        /// </summary>
        public void MenuItemCallback(object sender, EventArgs e)
        {
            _Dte.UndoContext.Open("Item renaming");
            Execute();
            _Dte.UndoContext.Close();
        }

        private void Execute()
        {
            BuildClassTree();
            _RuleSet = new CRenameRuleSet();
            SetNewNames(_RenameItems);
            CheckChanges();

            _RuleSet.Parameter.Prefix = "paramitr";
            _RuleSet.LokalVariable.Prefix = "lokVaritr";
            _RuleSet.LokalConst.Prefix = "lokConstitr";
            _RuleSet.Interface.Prefix = "I";
            _RuleSet.Class.Prefix = "C";
            _RuleSet.Enum.Prefix = "E";
            _RuleSet.Struct.Prefix = "S";

            /* public SRenameRule
            Parameter,
            LokalVariable,
            LokalConst,
            Interface,
            Class,
            Enum,
            Struct;
        public SRenameRule[]
            Const,
            Field,
            Property,
            Method;*/
            Message("\r\nFound local var types:");
            foreach (string type in _FoundTypes)
            {
                if (!_TypeResolver.IsType(type))
                    Message(type);
            }
            Message("IMPORTANT: Check if all found types above are actual types otherwhise use undo and fix addin!");
        }

        #region BuildClassTree
        private void BuildClassTree()
        {
            _RenameItems = new CRenameItemClass();
            _CurParent = _RenameItems;
            foreach (Project project in _Dte.Solution.Projects)
                IterateProjectItems(project.ProjectItems);
        }

        private void IterateProjectItems(ProjectItems projectItems)
        {
            foreach (ProjectItem item in projectItems)
            {
                if (item.Kind == Constants.vsProjectItemKindPhysicalFile && item.Name.EndsWith(".cs"))
                {
                    Message("File " + item.Name + ":");
                    IterateCodeElements(item.FileCodeModel.CodeElements, false);
                }
                if (item.SubProject != null)
                    IterateProjectItems(item.SubProject.ProjectItems);
                else
                    IterateProjectItems(item.ProjectItems);
            }
        }

        //Iterate through all the code elements in the provided element
        private void IterateCodeElements(CodeElements colCodeElements, bool gettingInherited)
        {
            //Check for nonmutable object inheritance
            if (colCodeElements == null)
                return;
            foreach (CodeElement objCodeElement in colCodeElements)
            {
                try
                {
                    CodeElement2 element = objCodeElement as CodeElement2;
                    CRenameItem cItem = null;
                    CRenameItemClassBase oldParent;
                    switch (element.Kind)
                    {
                        case vsCMElement.vsCMElementVariable:
                            cItem = new CRenameItem();
                            _CurParent.Variables.Add(cItem);
                            break;
                        case vsCMElement.vsCMElementProperty:
                            cItem = new CRenameItem();
                            _CurParent.Properties.Add(cItem);
                            break;
                        case vsCMElement.vsCMElementFunction:
                            cItem = new CRenameFunction();
                            CodeFunction2 func = (CodeFunction2)element;

                            if (!gettingInherited)
                            {
                                foreach (CodeElement2 param in func.Children)
                                {
                                    if (param.Kind == vsCMElement.vsCMElementParameter)
                                    {
                                        CRenameItem cItem2 = new CRenameItem {Name = param.Name, Element = param, Parent = (CRenameItemClass)_CurParent};
                                        ((CRenameFunction)cItem).Parameters.Add(cItem2);
                                    }
                                    else
                                        Message("Found a non parameter in method " + element.Name + ":" + param.Name + ":" + param.Kind);
                                }
                            }
                            _CurParent.Functions.Add((CRenameFunction)cItem);
                            break;
                        case vsCMElement.vsCMElementParameter:
                            throw new Exception("Found a parameter but did not expect one!");
                        case vsCMElement.vsCMElementInterface:
                            cItem = new CRenameItemClass();
                            _CurParent.Interfaces.Add((CRenameItemClass)cItem);
                            oldParent = _CurParent;
                            if (!gettingInherited)
                                _CurParent = (CRenameItemClass)cItem;
                            IterateCodeElements(((CodeInterface2)element).Members, gettingInherited);
                            if (!gettingInherited)
                                _CurParent = ((CRenameItemClass)cItem).InheritedStuff;
                            IterateCodeElements(((CodeInterface2)element).Bases, true);
                            _CurParent = oldParent;
                            break;
                        case vsCMElement.vsCMElementEnum:
                            cItem = new CRenameItemSub();
                            _CurParent.Enums.Add((CRenameItemSub)cItem);
                            break;
                        case vsCMElement.vsCMElementStruct:
                            cItem = new CRenameItemSub();
                            _CurParent.Structs.Add((CRenameItemSub)cItem);
                            break;
                        case vsCMElement.vsCMElementNamespace:
                            CodeNamespace objCodeNamespace = objCodeElement as CodeNamespace;
                            IterateCodeElements(objCodeNamespace.Members, gettingInherited);
                            break;
                        case vsCMElement.vsCMElementClass:
                            cItem = new CRenameItemClass();
                            _CurParent.Classes.Add((CRenameItemClass)cItem);
                            oldParent = _CurParent;
                            if (!gettingInherited)
                                _CurParent = (CRenameItemClass)cItem;
                            IterateCodeElements(((CodeClass2)element).Members, gettingInherited);
                            if (!gettingInherited)
                                _CurParent = ((CRenameItemClass)cItem).InheritedStuff;
                            IterateCodeElements(((CodeClass2)element).Bases, true);
                            _CurParent = oldParent;
                            break;
                    }
                    if (cItem != null)
                    {
                        try
                        {
                            cItem.Element = element;
                            cItem.Name = element.Name;
                            if (!gettingInherited)
                                cItem.Parent = (CRenameItemClass)_CurParent;
                        }
                        catch {}
                    }
                }
                catch {}
            }
        }
        #endregion

        #region GetNewName
        private string GetNewName(CodeFunction2 func)
        {
            return GetNewName((CodeElement2)func, _RuleSet.Field, func.Access);
        }

        private string GetNewName(CodeVariable2 variable)
        {
            if (variable.IsConstant)
                return GetNewName((CodeElement2)variable, _RuleSet.Const, variable.Access);
            return GetNewName((CodeElement2)variable, _RuleSet.Field, variable.Access);
        }

        private string GetNewName(CodeProperty2 property)
        {
            return GetNewName((CodeElement2)property, _RuleSet.Property, property.Access);
        }

        private string GetNewName(CodeElement2 element, SRenameRule[] rules, vsCMAccess access)
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

        private static string GetNewName(CodeElement2 element, SRenameRule rule)
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
            while (theString.StartsWith(prefix))
            {
                theString = theString.Remove(0, prefix.Length);
                if (prefix.Length == 1)
                    break;
            }
        }

        private static void ToLowerCamelCase(ref String theString)
        {
            ToCamelCase(ref theString);
            theString = theString.Substring(0, 1).ToLower() + theString.Substring(1);
        }

        private static void ToUpperCamelCase(ref String theString)
        {
            ToCamelCase(ref theString);
            theString = theString.Substring(0, 1).ToUpper() + theString.Substring(1);
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
        private void SetNewName(CRenameItem item, SRenameRule rule)
        {
            item.NewName = GetNewName(item.Name, rule);
        }

        private void SetNewNames(CRenameItemClass renameItem)
        {
            foreach (CRenameItemClass item in renameItem.Classes)
            {
                SetNewName(item, _RuleSet.Class);
                SetNewNames(item);
            }
            foreach (CRenameItemClass item in renameItem.Interfaces)
            {
                SetNewName(item, _RuleSet.Interface);
                SetNewNames(item);
            }
            foreach (CRenameItemSub item in renameItem.Enums)
                SetNewName(item, _RuleSet.Enum);
            foreach (CRenameItemSub item in renameItem.Structs)
                SetNewName(item, _RuleSet.Struct);

            foreach (CRenameItem item in renameItem.Properties)
                item.NewName = GetNewName((CodeProperty2)item.Element);
            foreach (CRenameItem item in renameItem.Variables)
                item.NewName = GetNewName((CodeVariable2)item.Element);
            foreach (CRenameFunction item in renameItem.Functions)
            {
                item.NewName = GetNewName((CodeFunction2)item.Element);
                foreach (CRenameItem param in item.Parameters)
                    SetNewName(param, _RuleSet.Parameter);
            }
        }
        #endregion

        private delegate bool HandleItem(CRenameItem item);

        private void RenameSymbol(CRenameItem item)
        {
            if (item.Name != item.NewName)
                item.Element.RenameSymbol(item.NewName);
        }

        private void ApplyChanges()
        {
            foreach (CRenameItemClass item in _RenameItems.Classes)
                RenameSymbol(item);
        }

        private bool CheckChanges()
        {
            return TraverseItems(CheckChanges);
        }

        private bool CheckChanges(CRenameItem item)
        {
            if (item.Name == item.NewName)
                return true;
            bool result;
            if (item is CRenameItemClass || item is CRenameItemSub)
                result = !item.Parent.IdExists(item.NewName, item.Name);
            else
                result = !item.Parent.MemberExists(item.NewName, item.Name);
            if (!result)
            {
                Message("Cannot rename " + item.Name + " to " + item.NewName);
                if (item.Element.ProjectItem.Document.Windows.Count == 0)
                    item.Element.ProjectItem.Open();
                item.Element.StartPoint.TryToShow(vsPaneShowHow.vsPaneShowTop);
            }
            return true;
        }

        private bool TraverseItems(HandleItem callBack)
        {
            return TraverseItems(_RenameItems, callBack);
        }

        private bool TraverseItems(CRenameItemClass itemClass, HandleItem callBack)
        {
            foreach (CRenameItemClass item in itemClass.Classes)
            {
                if (!callBack(item))
                    return false;
                TraverseItems(item, callBack);
            }
            foreach (CRenameItemClass item in itemClass.Interfaces)
            {
                if (!callBack(item))
                    return false;
                TraverseItems(item, callBack);
            }
            if (itemClass.Enums.Any(item => !callBack(item)))
                return false;
            if (itemClass.Structs.Any(item => !callBack(item)))
                return false;
            if (itemClass.Variables.Any(item => !callBack(item)))
                return false;
            if (itemClass.Properties.Any(item => !callBack(item)))
                return false;
            foreach (CRenameFunction item in itemClass.Functions)
            {
                if (item.Parameters.Any(param => !callBack(param)))
                    return false;
                if (!callBack(item))
                    return false;
            }
            return true;
        }

        private bool TypeExists(string type)
        {
            return _TypeResolver.IsType(type) || TypeExistsInClass(type, _RenameItems);
        }

        private bool TypeExistsInClass(string type, CRenameItemClass cItemClass)
        {
            if (cItemClass.Enums.Any(c => c.Name == type))
                return true;
            if (cItemClass.Structs.Any(c => c.Name == type))
                return true;
            foreach (CRenameItemClass c in cItemClass.Interfaces)
            {
                if (c.Name == type)
                    return true;
                if (TypeExistsInClass(type, c))
                    return true;
            }
            foreach (CRenameItemClass c in cItemClass.Classes)
            {
                if (c.Name == type)
                    return true;
                if (TypeExistsInClass(type, c))
                    return true;
            }
            return false;
        }

        private void RenameMethod(CRenameFunction func)
        {
            string funcText = "{" + func.GetText();
            func.RemoveTextComments(ref funcText);

            MatchCollection locVars = _ReLocVar.Matches(funcText);
            if (locVars.Count > 0)
            {
                Message("Method " + func.Name + ":");
                //First capture all vars, rename, check for 2 vars with same name and possible colliding space
                foreach (Match match in locVars)
                {
                    string type = match.Groups[1].Value.ToLower();
                    string name = match.Groups[5].Value;
                    if (type == "return" || type == "else" || type == "in" || type == "out" || type == "ref")
                        continue;
                    type = match.Groups[1].Value;
                    if (!_FoundTypes.Contains(type))
                        _FoundTypes.Add(type);
                    Message(name + "\t(" + type + ")");
                    string newName;
                    if (match.Groups[2].Value != "")
                        newName = GetNewName(name, _RuleSet.LokalConst);
                    else
                        newName = GetNewName(name, _RuleSet.LokalVariable);
                    if (name != newName)
                        funcText = Regex.Replace(funcText, @"(?<! new )(?<!\w|\.)" + Regex.Escape(name) + @"(?=( in )|\b(?!\s+[a-zA-Z_]))", newName, RegexOptions.Singleline);
                }

                func.RestoreTextComments(ref funcText);
                func.SetText(funcText.Substring(1));
            }
        }

        private void Message(string msg)
        {
            _OutputWindow.Activate();
            _OutputWindow.OutputString(msg + "\r\n");
        }
    }

    class CTypeResolver
    {
        private readonly Dictionary<string, Type> _Alias = new Dictionary<string, Type>();

        public CTypeResolver()
        {
            _Alias.Add("bool", typeof(bool));
            _Alias.Add("byte", typeof(byte));
            _Alias.Add("sbyte", typeof(sbyte));
            _Alias.Add("char", typeof(char));
            _Alias.Add("decimal", typeof(decimal));
            _Alias.Add("double", typeof(double));
            _Alias.Add("float", typeof(float));
            _Alias.Add("int", typeof(int));
            _Alias.Add("uint", typeof(uint));
            _Alias.Add("long", typeof(long));
            _Alias.Add("ulong", typeof(ulong));
            _Alias.Add("object", typeof(object));
            _Alias.Add("short", typeof(short));
            _Alias.Add("ushort", typeof(ushort));
            _Alias.Add("string", typeof(string));
        }

        public bool IsType(string typeName)
        {
            if (_Alias.ContainsKey(typeName.ToLower()))
                return true;
            return Type.GetType(typeName, false, true) != null;
        }
    }
}