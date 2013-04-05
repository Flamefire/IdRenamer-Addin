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

using System.Linq;
using EnvDTE;
using EnvDTE80;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NamingFix
{
    class CRenameItemMethod : CRenameItemElement, IRenameItemContainer
    {
        public readonly CRenameItemList<CRenameItemParameter> Parameters = new CRenameItemList<CRenameItemParameter>();
        private CRenameItemList<CRenameItemLocalVariable> _LocalVars;
        public CRenameItemList<CRenameItemLocalVariable> LocalVars
        {
            get
            {
                if (_LocalVars == null)
                    GetLocalVars();
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

        public bool IsDllImport()
        {
            return GetElement().Attributes.Cast<CodeAttribute>().Any(x => x.Name == "DllImport");
        }

        public override bool IsSystem
        {
            set
            {
                base.IsSystem = value;
                Parameters.ForEach(item => item.IsSystem = value);
                if (_LocalVars != null)
                    _LocalVars.ForEach(item => item.IsSystem = value);
            }
        }

        public CodeFunction2 GetElement()
        {
            return GetElement<CodeFunction2>();
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
            if (IsSystem || IsDllImport() || func.MustImplement)
                _Text = "";
            else
            {
                _StartPt = func.GetStartPoint(vsCMPart.vsCMPartBody).CreateEditPoint();
                _EndPt = func.GetEndPoint(vsCMPart.vsCMPartBody);
                String text = "{" + _StartPt.GetText(_EndPt);
                RemoveTextComments(ref text);
                _Text = text;
            }
        }

        public void ApplyNewText()
        {
            String text = Text;
            RestoreTextComments(ref text);
            _StartPt.ReplaceText(_EndPt, text.Substring(1), (int)vsEPReplaceTextOptions.vsEPReplaceTextKeepMarkers);
        }

        private void GetLocalVars()
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
                if (((CRenameItemInterfaceBase)Parent).FindTypeByName(type) == null && !CNamingFix.FoundTypes.Contains(type))
                    CNamingFix.FoundTypes.Add(type);
#if (DEBUG)
                CNamingFix.Message(name + "\t(" + type + ")");
#endif
                CRenameItemLocalVariable item = new CRenameItemLocalVariable {Name = name, Parent = this, IsConst = match.Groups[2].Value != ""};
                Add(item);
            }
        }

        private void RemoveTextComments(ref String text)
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

        private void RestoreTextComments(ref String text)
        {
            for (int i = 0; i < _Strings.Count; i++)
                text = text.Replace("\"ReplacedStr:::" + i + ":::\"", _Strings[i]);
            for (int i = 0; i < _Comments.Count; i++)
                text = text.Replace("//ReplacedCom:::" + i + ":::;\r\n", _Comments[i]);
        }

        public void Add(CRenameItem item)
        {
            if (item is CRenameItemParameter)
                Parameters.Add(item);
            else if (item is CRenameItemLocalVariable)
                LocalVars.Add(item);
            else
                throw new ArgumentException();
            item.Parent = this;
        }

        public CRenameItem GetConflictLocVar(string newName, string oldName)
        {
            CRenameItem item = Parameters.GetConflict(newName, oldName);
            return item ?? LocalVars.GetConflict(newName, oldName);
        }

        public CRenameItem GetConflictType(string newName, string oldName)
        {
            return Parent.GetConflictType(newName, oldName);
        }

        public CRenameItem GetConflictId(string newName, string oldName)
        {
            return Parent.GetConflictId(newName, oldName);
        }

        public override CRenameItem GetConflictItem()
        {
            if (Name == NewName)
                return null;
            CRenameItem item = Parent.GetConflictLocVar(NewName, Name);
            return item ?? Parent.GetConflictId(NewName, Name);
        }

        public CRenameItem FindTypeByName(string typeName)
        {
            //No types in methods
            return Parent.FindTypeByName(typeName);
        }
    }
}