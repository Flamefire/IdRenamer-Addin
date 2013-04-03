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

using System.Collections.Generic;
using System.Linq;
using System;
using EnvDTE80;

namespace NamingFix
{
    class CRenameItemInterfaceBase : CRenameItemType, IRenameItemContainer
    {
        public readonly CRenameItemList<CRenameItemMethod> Methods = new CRenameItemList<CRenameItemMethod>();
        public readonly CRenameItemList<CRenameItemProperty> Properties = new CRenameItemList<CRenameItemProperty>();
        public readonly CRenameItemList<CRenameItemEvent> Events = new CRenameItemList<CRenameItemEvent>();
        public bool IsTopClass;
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

        public virtual bool IsConflictLocVar(string newName, string oldName)
        {
            return Methods.Any(method => method.IsConflictLocVar(newName, oldName));
        }

        public virtual bool IsConflictType(string newName, string oldName)
        {
            return Events.IsConflict(newName, oldName) ||
                   Parent != null && Parent.IsConflictType(newName, oldName);
        }

        public virtual bool IsConflictId(string newName, string oldName)
        {
            return Properties.IsConflict(newName, oldName) ||
                   Methods.IsConflict(newName, oldName) ||
                   (Parent != null && Parent.IsConflictId(newName, oldName));
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

        protected static void SplitTypeName(string className, out string topClass, out String subClass)
        {
            int p = className.IndexOf('.');
            topClass = (p >= 0) ? className.Substring(0, p) : className;
            subClass = (p >= 0) ? className.Substring(p + 1) : "";
        }

        protected virtual CRenameItem FindTypeNameDown(String typeName)
        {
            return null;
        }

        public virtual CRenameItem FindTypeByName(string typeName)
        {
            //Strip redundant own typename
            //This is only valid where the id has been defined so do it here
            string mainType, subType;
            SplitTypeName(typeName, out mainType, out subType);
            if (mainType == Name)
            {
                if (subType == "")
                    return this;
                typeName = subType;
            }
            CRenameItem result = FindTypeNameDown(typeName);
            if (result != null)
                return result;
            if (Parent != null)
                return ((CRenameItemClass)Parent).FindTypeByName(typeName);
            //Assume we have a namespace and we are in top class
            return (subType == "" || !IsTopClass) ? null : FindTypeByName(subType);
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

        public override bool IsConflictLocVar(string newName, string oldName)
        {
            return base.IsConflictLocVar(newName, oldName) || _InheritedStuff.IsConflictLocVar(newName, oldName) || _DerivedStuff.IsConflictLocVar(newName, oldName);
        }

        public override bool IsConflictType(string newName, string oldName)
        {
            return base.IsConflictType(newName, oldName) || _InheritedStuff.IsConflictType(newName, oldName) || _DerivedStuff.IsConflictType(newName, oldName);
        }

        public override bool IsConflictId(string newName, string oldName)
        {
            return base.IsConflictId(newName, oldName) || _InheritedStuff.IsConflictId(newName, oldName) || _DerivedStuff.IsConflictId(newName, oldName);
        }
    }
}