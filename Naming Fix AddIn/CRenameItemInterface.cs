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

namespace NamingFix
{
    class CRenameItemInterfaceBase : CRenameItemType, IRenameItemContainer
    {
        public readonly CRenameItemList<CRenameItemMethod> Methods = new CRenameItemList<CRenameItemMethod>();
        public readonly CRenameItemList<CRenameItemProperty> Properties = new CRenameItemList<CRenameItemProperty>();
        public readonly CRenameItemList<CRenameItemEvent> Events = new CRenameItemList<CRenameItemEvent>();
        public bool IsInheritedLoaded;

        public override bool IsSystem
        {
            set
            {
                base.IsSystem = value;
                Methods.ForEach(item => item.IsSystem = value);
                Properties.ForEach(item => item.IsSystem = value);
            }
        }

        public virtual void Add(CRenameItem item)
        {
            if (item is CRenameItemMethod)
                Methods.Add(item);
            else if (item is CRenameItemProperty)
                Properties.Add(item);
            else if (item is CRenameItemEvent)
                Events.Add(item);
            else
                throw new ArgumentException();
            item.Parent = this;
            item.IsSystem = IsSystem;
        }

        public virtual CRenameItem GetConflictLocVar(string newName, string oldName)
        {
            //No local vars in interfaces
            return null;
        }

        public virtual CRenameItem GetConflictType(string newName, string oldName)
        {
            return Parent == null ? null : Parent.GetConflictType(newName, oldName);
        }

        public virtual CRenameItem GetConflictId(string newName, string oldName)
        {
            CRenameItem item = Properties.GetConflict(newName, oldName);
            if (item != null)
                return item;
            item = Methods.GetConflict(newName, oldName);
            if (item != null)
                return item;
            item = Events.GetConflict(newName, oldName);
            if (item != null)
                return item;
            return (Parent == null) ? null : Parent.GetConflictId(newName, oldName);
        }

        public virtual void CopyIds(CRenameItemInterfaceBase otherItem)
        {
            Methods.AddRange(otherItem.Methods);
            Properties.AddRange(otherItem.Properties);
        }

        public virtual void CopyIdsDerived(CRenameItemInterfaceBase otherItem)
        {
            throw new ArgumentException("Cannot copy derived IDs of base type");
        }

        public virtual CRenameItem FindTypeByName(string typeName)
        {
            return Parent != null ? Parent.FindTypeByName(typeName) : null;
        }
    }

    class CRenameItemInterface : CRenameItemInterfaceBase
    {
        private readonly CRenameItemInterfaceBase _InheritedStuff = new CRenameItemInterfaceBase();
        private readonly CRenameItemInterfaceBase _DerivedStuff = new CRenameItemInterfaceBase();

        public CodeInterface2 GetElement()
        {
            return GetElement<CodeInterface2>();
        }

        public override void CopyIds(CRenameItemInterfaceBase otherItem)
        {
            _InheritedStuff.CopyIds(otherItem);
            CRenameItemInterface otherItem2 = otherItem as CRenameItemInterface;
            if (otherItem2 != null)
                _InheritedStuff.CopyIds(otherItem2._InheritedStuff);
        }

        public override void CopyIdsDerived(CRenameItemInterfaceBase otherItem)
        {
            _DerivedStuff.CopyIds(otherItem);
            CRenameItemInterface otherItem2 = otherItem as CRenameItemInterface;
            if (otherItem2 != null)
                _DerivedStuff.CopyIds(otherItem2._DerivedStuff);
        }

        public override CRenameItem GetConflictLocVar(string newName, string oldName)
        {
            CRenameItem item = base.GetConflictLocVar(newName, oldName);
            if (item != null)
                return item;
            item = _InheritedStuff.GetConflictLocVar(newName, oldName);
            return item ?? _DerivedStuff.GetConflictLocVar(newName, oldName);
        }

        public override CRenameItem GetConflictType(string newName, string oldName)
        {
            CRenameItem item = base.GetConflictType(newName, oldName);
            if (item != null)
                return item;
            item = _InheritedStuff.GetConflictType(newName, oldName);
            return item ?? _DerivedStuff.GetConflictType(newName, oldName);
        }

        public override CRenameItem GetConflictId(string newName, string oldName)
        {
            CRenameItem item = base.GetConflictId(newName, oldName);
            if (item != null)
                return item;
            item = _InheritedStuff.GetConflictId(newName, oldName);
            return item ?? _DerivedStuff.GetConflictId(newName, oldName);
        }
    }
}