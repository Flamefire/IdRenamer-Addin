﻿#region license
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
        public readonly CRenameItemList<CRenameItemType> Types = new CRenameItemList<CRenameItemType>();
        public readonly CRenameItemList<CRenameItemVariable> Variables = new CRenameItemList<CRenameItemVariable>();

        public override bool IsSystem
        {
            set
            {
                base.IsSystem = value;
                Classes.ForEach(item => item.IsSystem = value);
                Interfaces.ForEach(item => item.IsSystem = value);
                Types.ForEach(item => item.IsSystem = value);
                Variables.ForEach(item => item.IsSystem = value);
            }
        }

        public override void Add(CRenameItem item)
        {
            if (item is CRenameItemClass)
                Classes.Add(item);
            else if (item is CRenameItemInterface)
                Interfaces.Add(item);
            else if (item is CRenameItemType)
                Types.Add(item);
            else if (item is CRenameItemVariable)
                Variables.Add(item);
            else
                base.Add(item);
            item.Parent = this;
            item.IsSystem = IsSystem;
        }

        public override bool IsConflictLocVar(string newName, string oldName)
        {
            return Methods.Any(method => method.IsConflictLocVar(newName, oldName));
        }

        public override bool IsConflictType(string newName, string oldName)
        {
            return Classes.IsConflict(newName, oldName) ||
                   Interfaces.IsConflict(newName, oldName) ||
                   Types.IsConflict(newName, oldName) ||
                   base.IsConflictType(newName, oldName);
        }

        public override bool IsConflictId(string newName, string oldName)
        {
            return Variables.IsConflict(newName, oldName) ||
                   base.IsConflictId(newName, oldName);
        }

        public override void CopyIds(CRenameItemInterfaceBase otherItem)
        {
            base.CopyIds(otherItem);
            CRenameItemClassBase otherItem2 = otherItem as CRenameItemClassBase;
            if (otherItem2 == null)
                return;
            Variables.AddRange(otherItem2.Variables);
            Classes.AddRange(otherItem2.Classes);
            Interfaces.AddRange(otherItem2.Interfaces);
            Types.AddRange(otherItem2.Types);
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
            return result ?? Types.Find(mainType);
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
        private readonly CRenameItemClassBase _InheritedStuff = new CRenameItemClassBase();
        private readonly CRenameItemClassBase _DerivedStuff = new CRenameItemClassBase();

        public CodeClass2 GetElement()
        {
            return GetElement<CodeClass2>();
        }

        public override void CopyIds(CRenameItemInterfaceBase otherItem)
        {
            _InheritedStuff.CopyIds(otherItem);
            CRenameItemClass otherItem2 = otherItem as CRenameItemClass;
            if (otherItem2 != null)
                _InheritedStuff.CopyIds(otherItem2._InheritedStuff);
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
            return result ?? _InheritedStuff.FindTypeByName(typeName);
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