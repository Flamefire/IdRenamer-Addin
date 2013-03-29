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
using System.Text.RegularExpressions;
using EnvDTE;
using EnvDTE80;

namespace NamingFix
{
    class CRenameFunction : CRenameItem
    {
        public readonly List<CRenameItemVariable> Parameters = new List<CRenameItemVariable>();
        public string Text = null;
        private EditPoint _StartPt;
        private TextPoint _EndPt;
        private readonly List<string> _Strings = new List<string>();
        private readonly List<string> _Comments = new List<string>();
        private static readonly Regex _ReString = new Regex(@"""(\\.|[^\\""])*""", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex _ReVarbatimString = new Regex("@\"(\"\"|[^\"])*\"", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex _ReCommentSl = new Regex(@"//[^\r\n]*\r\n", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex _ReCommentMl = new Regex(@"/\*.*?\*/", RegexOptions.Compiled | RegexOptions.Multiline);

        public CodeFunction2 GetElement()
        {
            return GetElement<CodeFunction2>();
        }

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
}