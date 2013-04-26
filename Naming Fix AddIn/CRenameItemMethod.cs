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
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace NamingFix
{
    class CRenameItemMethod : CRenameItemElement, IRenameItemContainer
    {
        private readonly CRenameItemList<CRenameItemParameter> _Parameters = new CRenameItemList<CRenameItemParameter>();
        private CRenameItemList<CRenameItemLocalVariable> _LocalVars;
        public CRenameItemList<CRenameItemLocalVariable> LocalVars
        {
            get
            {
                if (_LocalVars == null)
                    _GetLocalVars();
                return _LocalVars;
            }
        }
        private EditPoint _StartPt;
        private TextPoint _EndPt;
        private readonly List<string> _Strings = new List<string>();
        private readonly List<string> _Comments = new List<string>();
        private const string _Id = "@?[a-z_][a-z0-9_\\.]*";
        private static readonly Regex _ReLocVar = new Regex(@"(?<=[\{\(,;]\s*)((const\s+)?(" + _Id + ")(<" + _Id + @">)?)\s*(\[\])?\s+(" + _Id + @")(?=\s*([=,;]|( in )))",
                                                            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private static readonly Regex _ReString = new Regex(@"""(\\.|[^\\""])*""", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex _ReVarbatimString = new Regex("@\"(\"\"|[^\"])*\"", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex _ReCommentSl = new Regex(@"//[^\r\n]*\r\n", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex _ReCommentMl = new Regex(@"/\*.*?\*/", RegexOptions.Compiled | RegexOptions.Multiline);
        private string _ExplicitInterfaceName = "";

        public override string Name
        {
            set
            {
                string mainName, subName;
                CUtils.SplitTypeName(value, out mainName, out subName, false);
                if (subName != "")
                {
                    _ExplicitInterfaceName = mainName;
                    mainName = subName;
                }
                base.Name = mainName;
            }
        }

        public vsCMAccess Access
        {
            get { return (_ExplicitInterfaceName != "") ? vsCMAccess.vsCMAccessPublic : GetElement().Access; }
        }

        public override bool Rename()
        {
            if (_ExplicitInterfaceName != "")
            {
                if (NewName == Name)
                    return true;
                NewName = _ExplicitInterfaceName + "." + NewName;
            }
            return base.Rename();
        }

        private bool _IsExtern()
        {
            try
            {
                return GetElement().Attributes.Cast<CodeAttribute>().Any(x => x.Name == "DllImport" || x.Name == "MethodImpl");
            }
            catch (COMException)
            {
                return false;
            }
        }

        public override bool IsSystem
        {
            set
            {
                base.IsSystem = value;
                _Parameters.ForEach(item => item.IsSystem = value);
                if (_LocalVars != null)
                    _LocalVars.ForEach(item => item.IsSystem = value);
            }
        }

        public CodeFunction2 GetElement()
        {
            return _GetElement<CodeFunction2>();
        }

        private String _Text;
        public String Text
        {
            get
            {
                if (_Text == null)
                    ReloadText();
                return _Text;
            }
            set { _Text = value; }
        }

        public void ReloadText()
        {
            CodeFunction2 func = GetElement();
            if (IsSystem || _IsExtern() || func.MustImplement)
                _Text = "";
            else
            {
                _StartPt = func.GetStartPoint(vsCMPart.vsCMPartBody).CreateEditPoint();
                _EndPt = func.GetEndPoint(vsCMPart.vsCMPartBody);
                String text = "{" + _StartPt.GetText(_EndPt);
                _RemoveTextComments(ref text);
                _Text = text;
            }
        }

        public void ApplyNewText()
        {
            String text = Text;
            if (String.IsNullOrEmpty(text))
                return;
            _RestoreTextComments(ref text);
            _StartPt.ReplaceText(_EndPt, text.Substring(1), (int)vsEPReplaceTextOptions.vsEPReplaceTextKeepMarkers);
        }

        private void _GetLocalVars()
        {
            _LocalVars = new CRenameItemList<CRenameItemLocalVariable>();
            MatchCollection locVars = _ReLocVar.Matches(Text);
            if (locVars.Count <= 0)
                return;
#if (DEBUG)
            CNamingFix.Message("Method " + Name + ":");
#endif
            //First capture all vars, rename, check for 2 vars with same name and possible colliding space
            foreach (Match match in locVars)
            {
                string type = match.Groups[3].Value;
                string name = match.Groups[6].Value;
                if (type == "return" || type == "else" || type == "in" || type == "out" || type == "ref")
                    continue;
                if (Parent.FindTypeByName(type) == null && !CNamingFix.FoundTypes.Contains(type))
                    CNamingFix.FoundTypes.Add(type);
#if (DEBUG)
                CNamingFix.Message(name + "\t(" + type + ")");
#endif
                CRenameItemLocalVariable item = new CRenameItemLocalVariable {Name = name, Parent = this, IsConst = match.Groups[2].Value != ""};
                Add(item);
            }
        }

        private void _RemoveTextComments(ref String text)
        {
            _Strings.Clear();
            _Comments.Clear();
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
            text = _ReVarbatimString.Replace(text, delegate(Match m)
            {
                _Strings.Add(m.Value);
                return "\"ReplacedStr:::" + (_Strings.Count - 1) + ":::\"";
            });
            text = _ReString.Replace(text, delegate(Match m)
                {
                    _Strings.Add(m.Value);
                    return "\"ReplacedStr:::" + (_Strings.Count - 1) + ":::\"";
                });
        }

        private void _RestoreTextComments(ref String text)
        {
            for (int i = _Strings.Count - 1; i >= 0; i++)
                text = text.Replace("\"ReplacedStr:::" + i + ":::\"", _Strings[i]);
            for (int i = _Comments.Count - 1; i >= 0; i++)
                text = text.Replace("//ReplacedCom:::" + i + ":::;\r\n", _Comments[i]);
        }

        public void Add(CRenameItem item)
        {
            if (item is CRenameItemParameter)
                _Parameters.Add(item);
            else if (item is CRenameItemLocalVariable)
                LocalVars.Add(item);
            else
                throw new ArgumentException();
            item.Parent = this;
            item.IsSystem = IsSystem;
        }

        public CRenameItem GetConflictLocVar(string newName, string oldName, bool swapCheck)
        {
            CRenameItem item = _Parameters.GetConflict(newName, oldName, swapCheck);
            return item ?? LocalVars.GetConflict(newName, oldName, swapCheck);
        }

        public CRenameItem GetConflictType(string newName, string oldName, bool swapCheck)
        {
            return Parent.GetConflictType(newName, oldName, swapCheck);
        }

        public CRenameItem GetConflictId(string newName, string oldName, bool swapCheck)
        {
            return Parent.GetConflictId(newName, oldName, swapCheck);
        }

        public override CRenameItem GetConflictItem(bool swapCheck)
        {
            if (Name == NewName)
                return null;
            CRenameItem item = Parent.GetConflictLocVar(NewName, Name, swapCheck);
            return item ?? Parent.GetConflictId(NewName, Name, swapCheck);
        }

        public CRenameItem FindTypeByName(string typeName)
        {
            //No types in methods
            return Parent.FindTypeByName(typeName);
        }

        public override bool IsRenamingAllowed()
        {
            //Just rename methods that are not extern and no constructors/destructors and not "this" (special list id)
            return base.IsRenamingAllowed() &&
                   !_IsExtern() &&
                   !Name.StartsWith("~") &&
                   Name != Parent.Name &&
                   Name != "Main" &&
                   !_IsOverrideConflict();
        }

        private bool _IsOverrideConflict()
        {
            CRenameItemList<CRenameItemMethod> inheritedMethods;
            CRenameItemInterface parentInterface = Parent as CRenameItemInterface;
            if (parentInterface != null)
                inheritedMethods = parentInterface.InheritedStuff.Methods;
            else
            {
                CRenameItemClass parentClass = Parent as CRenameItemClass;
                if (parentClass == null)
                    return false;
                inheritedMethods = parentClass.InheritedStuff.Methods;
            }
            return inheritedMethods.Any(method => method.Name == Name && !method.IsRenamingAllowed());
        }

        public IEnumerator GetEnumerator()
        {
            return new List<IEnumerable> {_Parameters, LocalVars}.GetEnumerator();
        }
    }
}