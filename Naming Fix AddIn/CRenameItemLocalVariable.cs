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
using System.Text.RegularExpressions;

namespace NamingFix
{
    class CRenameItemLocalVariable : CRenameItem
    {
        public bool IsConst;
        public new CRenameItemMethod Parent
        {
            private get { return (CRenameItemMethod)base.Parent; }
            set { base.Parent = value; }
        }
        public override ProjectItem ProjectItem
        {
            get { return Parent.ProjectItem; }
        }
        public override TextPoint StartPoint
        {
            get { return Parent.StartPoint; }
        }

        public override void Rename()
        {
            if (Name == NewName)
                return;
            CRenameItemMethod parent = Parent;
            string nameRe = Regex.Escape(Name);
            if (nameRe[0] == '@')
                nameRe = "@?" + nameRe.Substring(1);
            parent.Text = Regex.Replace(parent.Text, @"(?<! new )(?<!\w|\.)" + nameRe + @"(?=( in )|\b(?!\s+[a-zA-Z_]))", NewName, RegexOptions.Singleline);
        }

        public override CRenameItem GetConflictItem(bool swapCheck)
        {
            if (Name == NewName)
                return null;
            CRenameItem item = Parent.GetConflictLocVar(NewName, Name, swapCheck);
            return item ?? Parent.GetConflictId(NewName, Name, swapCheck);
        }
    }
}