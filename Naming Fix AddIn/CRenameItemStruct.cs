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
using System.Linq;

namespace NamingFix
{
    class CRenameItemStruct : CRenameItemType
    {
        private readonly CRenameItemList<CRenameItemVariable> _Variables = new CRenameItemList<CRenameItemVariable>();
        private readonly CRenameItemList<CRenameItemMethod> _Methods = new CRenameItemList<CRenameItemMethod>();
        private readonly CRenameItemList<CRenameItemProperty> _Properties = new CRenameItemList<CRenameItemProperty>();

        public CodeStruct GetElement()
        {
            return _GetElement<CodeStruct>();
        }

        public override void Add(CRenameItem item)
        {
            if (item is CRenameItemMethod)
                _Methods.Add(item);
            else if (item is CRenameItemProperty)
                _Properties.Add(item);
            else if (item is CRenameItemVariable)
                _Variables.Add(item);
            else
                throw new ArgumentException();
            item.Parent = this;
            item.IsSystem = IsSystem;
        }

        public override CRenameItem GetConflictLocVar(string newName, string oldName, bool swapCheck)
        {
            return _Methods.Select(method => method.GetConflictLocVar(newName, oldName, swapCheck)).FirstOrDefault(item => item != null);
        }

        public override CRenameItem GetConflictType(string newName, string oldName, bool swapCheck)
        {
            return Parent.GetConflictType(newName, oldName, swapCheck);
        }

        public override CRenameItem GetConflictId(string newName, string oldName, bool swapCheck)
        {
            CRenameItem item = _Properties.GetConflict(newName, oldName, swapCheck);
            if (item != null)
                return item;
            item = _Methods.GetConflict(newName, oldName, swapCheck);
            if (item != null)
                return item;
            item = _Variables.GetConflict(newName, oldName, swapCheck);
            return item ?? Parent.GetConflictId(newName, oldName, swapCheck);
        }

        public override CRenameItem FindTypeByName(string typeName)
        {
            return Parent.FindTypeByName(typeName);
        }

        public override IEnumerator GetEnumerator()
        {
            return new List<IEnumerable> {_Variables, _Methods, _Properties}.GetEnumerator();
        }
    }
}