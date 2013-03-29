﻿#region license
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
                    IterateCodeElements(item.FileCodeModel.CodeElements, _RenameItems);
                }
                if (item.SubProject != null)
                    IterateProjectItems(item.SubProject.ProjectItems);
                else
                    IterateProjectItems(item.ProjectItems);
            }
        }

        //Iterate through all the code elements in the provided element
        private void IterateCodeElements(CodeElements colCodeElements, IRenameItemInterface curParent)
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
                    switch (element.Kind)
                    {
                        case vsCMElement.vsCMElementVariable:
                            cItem = new CRenameItemVariable();
                            curParent.AddVar(cItem);
                            break;
                        case vsCMElement.vsCMElementProperty:
                            cItem = new CRenameItemVariable();
                            curParent.AddProperty(cItem);
                            break;
                        case vsCMElement.vsCMElementFunction:
                            cItem = new CRenameFunction();
                            CodeFunction2 func = (CodeFunction2)element;

                            foreach (CodeElement2 param in func.Children)
                            {
                                if (param.Kind == vsCMElement.vsCMElementParameter)
                                {
                                    CRenameItemVariable cItem2 = new CRenameItemVariable {Name = param.Name, Element = param, Parent = (CRenameItemClass)curParent};
                                    ((CRenameFunction)cItem).Parameters.Add(cItem2);
                                }
                                else
                                    Message("Found a non parameter in method " + element.Name + ":" + param.Name + ":" + param.Kind);
                            }
                            curParent.AddFunc(cItem);
                            break;
                        case vsCMElement.vsCMElementParameter:
                            throw new Exception("Found a parameter but did not expect one!");
                        case vsCMElement.vsCMElementInterface:
                            cItem = new CRenameItemInterface();
                            curParent.AddInterface(cItem);
                            IterateCodeElements(((CodeInterface2)element).Members, (CRenameItemInterface)cItem);
                            break;
                        case vsCMElement.vsCMElementEnum:
                            cItem = new CRenameItemEnum();
                            curParent.AddEnum(cItem);
                            break;
                        case vsCMElement.vsCMElementStruct:
                            cItem = new CRenameItemStruct();
                            curParent.AddStruct(cItem);
                            break;
                        case vsCMElement.vsCMElementNamespace:
                            CodeNamespace objCodeNamespace = objCodeElement as CodeNamespace;
                            IterateCodeElements(objCodeNamespace.Members, curParent);
                            break;
                        case vsCMElement.vsCMElementClass:
                            cItem = new CRenameItemClass();
                            curParent.AddClass(cItem);
                            IterateCodeElements(((CodeClass2)element).Members, (CRenameItemClass)cItem);
                            break;
                    }
                    if (cItem != null)
                    {
                        try
                        {
                            cItem.Element = element;
                            cItem.Name = element.Name;
                            cItem.Parent = (CRenameItemClass)curParent;
                        }
                        catch {}
                    }
                }
                catch {}
            }
        }

        private void GetInherited(CRenameItemClass child) {}
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

        private void SetNewNames(CRenameItemInterfaceBase renameItem)
        {
            CRenameItemClass itemClass = renameItem as CRenameItemClass;
            if (itemClass != null)
            {
                foreach (var item in itemClass.Classes)
                {
                    SetNewName(item, _RuleSet.Class);
                    SetNewNames(item);
                }
                foreach (var item in itemClass.Interfaces)
                {
                    SetNewName(item, _RuleSet.Interface);
                    SetNewNames(item);
                }
                foreach (var item in itemClass.Enums)
                    SetNewName(item, _RuleSet.Enum);
                foreach (var item in itemClass.Structs)
                    SetNewName(item, _RuleSet.Struct);
                foreach (var item in itemClass.Variables)
                    item.NewName = GetNewName((CodeVariable2)item.Element);
            }

            foreach (var item in renameItem.Properties)
                item.NewName = GetNewName((CodeProperty2)item.Element);
            foreach (CRenameFunction item in renameItem.Functions)
            {
                item.NewName = GetNewName((CodeFunction2)item.Element);
                foreach (var param in item.Parameters)
                    SetNewName(param, _RuleSet.Parameter);
            }
        }
        #endregion

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
            //No renaming is ok
            if (item.Name == item.NewName)
                return true;
            bool result;
            if (item is CRenameItemType)
                //if item is a type then thre must not be another id with same name in that scope
                result = !item.Parent.IdCollidesWithId(item.NewName, item.Name);
            else
                //if item is a variable, property or function then there must be no other v, p or f
                result = !item.Parent.IdCollidesWithMember(item.NewName, item.Name);
            if (!result)
            {
                Message("Cannot rename " + item.Name + " to " + item.NewName + " as it would be the same name as another item");
                if (item.Element.ProjectItem.Document.Windows.Count == 0)
                    item.Element.ProjectItem.Open();
                item.Element.StartPoint.TryToShow(vsPaneShowHow.vsPaneShowTop);
            }
            return true;
        }

        private delegate bool HandleItem(CRenameItem item);

        private bool TraverseItems(HandleItem callBack)
        {
            return TraverseItems(_RenameItems, callBack);
        }

        private bool TraverseItems(CRenameItemInterfaceBase itemInterface, HandleItem callBack)
        {
            CRenameItemClass itemClass = itemInterface as CRenameItemClass;
            if (itemClass != null)
            {
                foreach (CRenameItemClass item in itemClass.Classes)
                {
                    if (!callBack(item))
                        return false;
                    TraverseItems(item, callBack);
                }
                foreach (CRenameItemInterface item in itemClass.Interfaces)
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
            }
            if (itemInterface.Properties.Any(item => !callBack(item)))
                return false;
            foreach (CRenameFunction item in itemInterface.Functions)
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
            return _TypeResolver.IsType(type) || _RenameItems.FindTypeName(type) != null;
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