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
using EnvDTE80;

namespace NamingFix
{
    /// <summary>
    ///     Do not set Parent to anything!
    /// </summary>
    class CRenameItemClassBase : CRenameItemInterfaceBase
    {
        public bool IsTopClass;
        public readonly CRenameItemList<CRenameItemClass> Classes = new CRenameItemList<CRenameItemClass>();
        public readonly CRenameItemList<CRenameItemInterface> Interfaces = new CRenameItemList<CRenameItemInterface>();
        public readonly CRenameItemList<CRenameItemStruct> Structs = new CRenameItemList<CRenameItemStruct>();
        public readonly CRenameItemList<CRenameItemEnum> Enums = new CRenameItemList<CRenameItemEnum>();
        public readonly CRenameItemList<CRenameItemDelegate> Delegates = new CRenameItemList<CRenameItemDelegate>();
        public readonly CRenameItemList<CRenameItemVariable> Variables = new CRenameItemList<CRenameItemVariable>();

        public override void Add(CRenameItem item)
        {
            if (item is CRenameItemClass)
                Classes.Add(item);
            else if (item is CRenameItemInterface)
                Interfaces.Add(item);
            else if (item is CRenameItemEnum)
                Enums.Add(item);
            else if (item is CRenameItemStruct)
                Structs.Add(item);
            else if (item is CRenameItemDelegate)
                Delegates.Add(item);
            else if (item is CRenameItemVariable)
                Variables.Add(item);
            else
                base.Add(item);
            item.Parent = this;
        }

        public override void CopyIds(CRenameItemInterfaceBase otherItem)
        {
            base.CopyIds(otherItem);
            CRenameItemClassBase otherItem2 = otherItem as CRenameItemClassBase;
            if (otherItem2 == null)
                return;
            AddUniqueItems(Variables, otherItem2.Variables, otherItem2.ReadOnly);
            AddUniqueItems(Classes, otherItem2.Classes, otherItem2.ReadOnly);
            AddUniqueItems(Interfaces, otherItem2.Interfaces, otherItem2.ReadOnly);
            AddUniqueItems(Enums, otherItem2.Enums, otherItem2.ReadOnly);
            AddUniqueItems(Structs, otherItem2.Structs, otherItem2.ReadOnly);
            AddUniqueItems(Delegates, otherItem2.Delegates, otherItem2.ReadOnly);
        }

        public override bool IsMemberRenameValid(string newName, string oldName)
        {
            return base.IsMemberRenameValid(newName, oldName) &&
                   Variables.IsRenameValid(newName, oldName);
        }

        public override bool IsIdRenameValid(string newName, string oldName)
        {
            return base.IsIdRenameValid(newName, oldName) &&
                   Classes.IsRenameValid(newName, oldName) &&
                   Interfaces.IsRenameValid(newName, oldName) &&
                   Structs.IsRenameValid(newName, oldName) &&
                   Enums.IsRenameValid(newName, oldName) &&
                   Delegates.IsRenameValid(newName, oldName);
        }

        private static void SplitTypeName(string className, out string topClass, out String subClass)
        {
            int p = className.IndexOf('.');
            topClass = (p >= 0) ? className.Substring(0, p) : className;
            subClass = (p >= 0) ? className.Substring(p + 1) : "";
        }

        private CRenameItem FindTypeNameDown(String typeName)
        {
            string mainType, subType;
            SplitTypeName(typeName, out mainType, out subType);

            CRenameItemClass cClass = Classes.Find(mainType);
            if (subType != "")
            {
                //Only classes can have subTypes so either get down in this class or exit
                return cClass != null ? cClass.FindTypeNameDown(subType) : null;
            }
            if (cClass != null)
                return cClass;

            CRenameItem result = Interfaces.Find(mainType);
            if (result != null)
                return result;
            result = Structs.Find(mainType);
            if (result != null)
                return result;
            result = Enums.Find(mainType);
            if (result != null)
                return result;
            result = Delegates.Find(mainType);

            return result;
        }

        public override CRenameItem FindTypeByName(string typeName)
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

    class CRenameItemClass : CRenameItemClassBase
    {
        public CRenameItemClassBase InheritedStuff = new CRenameItemClassBase();

        public bool IsInheritedLoaded;

        public override bool ReadOnly
        {
            set
            {
                base.ReadOnly = value;
                InheritedStuff.ReadOnly = value;
            }
        }

        public CodeClass2 GetElement()
        {
            return GetElement<CodeClass2>();
        }

        public override void CopyIds(CRenameItemInterfaceBase otherItem)
        {
            InheritedStuff.CopyIds(otherItem);
            CRenameItemClass otherItem2 = otherItem as CRenameItemClass;
            if (otherItem2 != null)
                InheritedStuff.CopyIds(otherItem2.InheritedStuff);
        }

        public override bool IsMemberRenameValid(string newName, string oldName)
        {
            return base.IsMemberRenameValid(newName, oldName) || (InheritedStuff != null && InheritedStuff.IsMemberRenameValid(newName, oldName));
        }

        public override bool IsIdRenameValid(string newName, string oldName)
        {
            return base.IsIdRenameValid(newName, oldName) || (InheritedStuff != null && InheritedStuff.IsIdRenameValid(newName, oldName));
        }

        public override CRenameItem FindTypeByName(string typeName)
        {
            CRenameItem result = base.FindTypeByName(typeName);
            if (result != null)
                return result;
            return InheritedStuff != null ? InheritedStuff.FindTypeByName(typeName) : null;
        }
    }
}