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
using System;
using System.Collections;
using System.Collections.Generic;

namespace NamingFix
{
    class CRenameItemEnum : CRenameItemType
    {
        private readonly CRenameItemList<CRenameItemEnumMember> _EnumMembers = new CRenameItemList<CRenameItemEnumMember>();

        public CodeEnum GetElement()
        {
            return _GetElement<CodeEnum>();
        }

        public override void Add(CRenameItem item)
        {
            if (item is CRenameItemEnumMember)
                _EnumMembers.Add(item);
            else
                throw new ArgumentException();
            item.Parent = this;
            item.IsSystem = IsSystem;
        }

        public override CRenameItem GetConflictLocVar(string newName, string oldName, bool swapCheck)
        {
            return null;
        }

        public override CRenameItem GetConflictType(string newName, string oldName, bool swapCheck)
        {
            return Parent.GetConflictType(newName, oldName, swapCheck);
        }

        public override CRenameItem GetConflictId(string newName, string oldName, bool swapCheck)
        {
            return Parent.GetConflictId(newName, oldName, swapCheck);
        }

        public override CRenameItem FindTypeByName(string typeName)
        {
            //No types in methods
            return Parent.FindTypeByName(typeName);
        }

        public override IEnumerator GetEnumerator()
        {
            return new List<IEnumerable> {_EnumMembers}.GetEnumerator();
        }
    }
}