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
using System.Linq;
using EnvDTE80;

namespace NamingFix
{
    /// <summary>
    ///     Do not set Parent to anything!
    /// </summary>
    class CRenameItemClassBase : CRenameItemInterfaceBase
    {
        public readonly List<CRenameItemClass> Classes = new List<CRenameItemClass>();
        public readonly List<CRenameItemInterface> Interfaces = new List<CRenameItemInterface>();
        public readonly List<CRenameItemStruct> Structs = new List<CRenameItemStruct>();
        public readonly List<CRenameItemEnum> Enums = new List<CRenameItemEnum>();
        public readonly List<CRenameItemVariable> Variables = new List<CRenameItemVariable>();

        public override void AddVar(CRenameItem var)
        {
            Variables.Add((CRenameItemVariable)var);
        }

        public override void AddClass(CRenameItem type)
        {
            Classes.Add((CRenameItemClass)type);
        }

        public override void AddInterface(CRenameItem type)
        {
            Interfaces.Add((CRenameItemInterface)type);
        }

        public override void AddEnum(CRenameItem type)
        {
            Enums.Add((CRenameItemEnum)type);
        }

        public override void AddStruct(CRenameItem type)
        {
            Structs.Add((CRenameItemStruct)type);
        }

        public override void CopyIds(CRenameItemInterfaceBase otherItem, bool readOnly = false)
        {
            base.CopyIds(otherItem, readOnly);
            var otherItem2 = otherItem as CRenameItemClassBase;
            if (otherItem2 == null)
                return;
            AddUniqueItems(Variables, otherItem2.Variables, otherItem2.ReadOnly || readOnly);
            AddUniqueItems(Classes, otherItem2.Classes, otherItem2.ReadOnly || readOnly);
            AddUniqueItems(Interfaces, otherItem2.Interfaces, otherItem2.ReadOnly || readOnly);
            AddUniqueItems(Enums, otherItem2.Enums, otherItem2.ReadOnly || readOnly);
            AddUniqueItems(Structs, otherItem2.Structs, otherItem2.ReadOnly || readOnly);
        }

        public override bool IdCollidesWithMember(string newName, string oldName)
        {
            return base.IdCollidesWithMember(newName, oldName) ||
                   Variables.Any(item => item.NewName == newName && item.Name != oldName);
        }

        public override bool IdCollidesWithId(string newName, string oldName)
        {
            return base.IdCollidesWithId(newName, oldName) ||
                   Classes.Any(item => item.NewName == newName && item.Name != oldName) ||
                   Interfaces.Any(item => item.NewName == newName && item.Name != oldName) ||
                   Structs.Any(item => item.NewName == newName && item.Name != oldName) ||
                   Enums.Any(item => item.NewName == newName && item.Name != oldName);
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

            CRenameItemClass cClass = Classes.FirstOrDefault(item => item.Name == mainType);
            if (subType != "")
            {
                //Only classes can have subTypes so either get down in this class or exit
                return cClass != null ? cClass.FindTypeNameDown(subType) : null;
            }
            if (cClass != null)
                return cClass;

            CRenameItem result = Interfaces.FirstOrDefault(item => item.Name == mainType);
            if (result != null)
                return result;
            result = Structs.FirstOrDefault(item => item.Name == mainType);
            if (result != null)
                return result;
            result = Enums.FirstOrDefault(item => item.Name == mainType);

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
            return Parent != null ? ((CRenameItemClass)Parent).FindTypeByName(typeName) : null;
        }
    }

    class CRenameItemClass : CRenameItemClassBase
    {
        public CRenameItemClassBase InheritedStuff;

        public CodeClass2 GetElement()
        {
            return GetElement<CodeClass2>();
        }

        public override void CopyIds(CRenameItemInterfaceBase otherItem, bool readOnly = false)
        {
            InheritedStuff.CopyIds(otherItem, readOnly);
            var otherItem2 = otherItem as CRenameItemClass;
            if (otherItem2 != null)
                InheritedStuff.CopyIds(otherItem2.InheritedStuff, otherItem2.ReadOnly || readOnly);
        }

        public override bool IdCollidesWithMember(string newName, string oldName)
        {
            return base.IdCollidesWithMember(newName, oldName) || (InheritedStuff != null && InheritedStuff.IdCollidesWithMember(newName, oldName));
        }

        public override bool IdCollidesWithId(string newName, string oldName)
        {
            return base.IdCollidesWithId(newName, oldName) || (InheritedStuff != null && InheritedStuff.IdCollidesWithId(newName, oldName));
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