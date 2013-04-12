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

using EnvDTE80;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace NamingFix
{
    /// <summary>
    ///     Do not set Parent to anything!
    /// </summary>
    class CRenameItemClassBase : CRenameItemInterfaceBase
    {
        public readonly CRenameItemList<CRenameItemClass> Classes = new CRenameItemList<CRenameItemClass>();
        public readonly CRenameItemList<CRenameItemInterface> Interfaces = new CRenameItemList<CRenameItemInterface>();
        private readonly CRenameItemList<CRenameItemType> _Types = new CRenameItemList<CRenameItemType>();
        private readonly CRenameItemList<CRenameItemVariable> _Variables = new CRenameItemList<CRenameItemVariable>();

        public override void Add(CRenameItem item)
        {
            if (item is CRenameItemClass)
                Classes.Add(item);
            else if (item is CRenameItemInterface)
                Interfaces.Add(item);
            else if (item is CRenameItemType)
                _Types.Add(item);
            else if (item is CRenameItemVariable)
                _Variables.Add(item);
            else
                base.Add(item);
            item.Parent = this;
            item.IsSystem = IsSystem;
        }

        protected override List<IEnumerable> _GetEnumeratorList()
        {
            List<IEnumerable> result = base._GetEnumeratorList();
            result.Add(Classes);
            result.Add(Interfaces);
            result.Add(_Types);
            result.Add(_Variables);
            return result;
        }

        public override CRenameItem GetConflictLocVar(string newName, string oldName, bool swapCheck)
        {
            CRenameItem result = Methods.Select(method => method.GetConflictLocVar(newName, oldName, swapCheck)).FirstOrDefault(item => item != null);
            return result ?? _Types.Select(type => type.GetConflictLocVar(newName, oldName, swapCheck)).FirstOrDefault(item => item != null);
        }

        public override CRenameItem GetConflictType(string newName, string oldName, bool swapCheck)
        {
            CRenameItem item = Classes.GetConflict(newName, oldName, swapCheck);
            if (item != null)
                return item;
            item = Interfaces.GetConflict(newName, oldName, swapCheck);
            if (item != null)
                return item;
            item = _Types.GetConflict(newName, oldName, swapCheck);
            return item ?? base.GetConflictType(newName, oldName, swapCheck);
        }

        public override CRenameItem GetConflictId(string newName, string oldName, bool swapCheck)
        {
            CRenameItem item = _Variables.GetConflict(newName, oldName, swapCheck);
            return item ?? base.GetConflictId(newName, oldName, swapCheck);
        }

        public override void CopyIds(CRenameItemInterfaceBase otherItem)
        {
            base.CopyIds(otherItem);
            CRenameItemClassBase otherItem2 = otherItem as CRenameItemClassBase;
            if (otherItem2 == null)
                return;
            _Variables.AddRange(otherItem2._Variables);
            Classes.AddRange(otherItem2.Classes);
            Interfaces.AddRange(otherItem2.Interfaces);
            _Types.AddRange(otherItem2._Types);
        }

        public CRenameItem FindTypeNameDown(String typeName)
        {
            string mainType, subType;
            CUtils.SplitTypeName(typeName, out mainType, out subType);

            CRenameItemClass cClass = Classes.Find(mainType);
            if (subType != "")
            {
                //Only classes can have subTypes so either get down in this class or exit
                return cClass != null ? cClass.FindTypeNameDown(subType) : null;
            }
            if (cClass != null)
                return cClass;

            CRenameItem result = Interfaces.Find(mainType);
            return result ?? _Types.Find(mainType);
        }

        public override CRenameItem FindTypeByName(string typeName)
        {
            CRenameItem result = FindTypeNameDown(typeName);
            if (result != null)
                return result;
            return base.FindTypeByName(typeName);
        }
    }

    class CRenameItemClass : CRenameItemClassBase
    {
        public readonly CRenameItemClassBase InheritedStuff = new CRenameItemClassBase();
        private readonly CRenameItemClassBase _DerivedStuff = new CRenameItemClassBase();

        public CodeClass2 GetElement()
        {
            return _GetElement<CodeClass2>();
        }

        public override void CopyIds(CRenameItemInterfaceBase otherItem)
        {
            InheritedStuff.CopyIds(otherItem);
            CRenameItemClass otherItem2 = otherItem as CRenameItemClass;
            if (otherItem2 != null)
                InheritedStuff.CopyIds(otherItem2.InheritedStuff);
        }

        public override void CopyIdsDerived(CRenameItemInterfaceBase otherItem)
        {
            _DerivedStuff.CopyIds(otherItem);
            CRenameItemClass otherItem2 = otherItem as CRenameItemClass;
            if (otherItem2 != null)
                _DerivedStuff.CopyIds(otherItem2._DerivedStuff);
        }

        public override CRenameItem FindTypeByName(string typeName)
        {
            CRenameItem result = base.FindTypeByName(typeName);
            return result ?? InheritedStuff.FindTypeByName(typeName);
        }

        public override CRenameItem GetConflictLocVar(string newName, string oldName, bool swapCheck)
        {
            CRenameItem item = base.GetConflictLocVar(newName, oldName, swapCheck);
            if (item != null)
                return item;
            item = InheritedStuff.GetConflictLocVar(newName, oldName, swapCheck);
            return item ?? _DerivedStuff.GetConflictLocVar(newName, oldName, swapCheck);
        }

        public override CRenameItem GetConflictType(string newName, string oldName, bool swapCheck)
        {
            CRenameItem item = base.GetConflictType(newName, oldName, swapCheck);
            if (item != null)
                return item;
            item = InheritedStuff.GetConflictType(newName, oldName, swapCheck);
            return item ?? _DerivedStuff.GetConflictType(newName, oldName, swapCheck);
        }

        public override CRenameItem GetConflictId(string newName, string oldName, bool swapCheck)
        {
            CRenameItem item = base.GetConflictId(newName, oldName, swapCheck);
            if (item != null)
                return item;
            item = InheritedStuff.GetConflictId(newName, oldName, swapCheck);
            return item ?? _DerivedStuff.GetConflictId(newName, oldName, swapCheck);
        }
    }
}