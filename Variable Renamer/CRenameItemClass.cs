#region license
/*
    This file is part of the item renamer Add-In for VS ("Add-In").

    The Add-In is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    The Add-In is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with Foobar.  If not, see <http://www.gnu.org/licenses/>.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using EnvDTE80;

namespace Variable_Renamer
{
    /// <summary>
    /// Do not set Parent to anything!
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
            if (typeName == Name)
                return this;

            string topClass, subClass;
            SplitTypeName(typeName, out topClass, out subClass);

            CRenameItem result;

            foreach (var item in Classes.Where(item => item.Name == topClass))
            {
                result = item.FindTypeNameDown(subClass);
                if (result != null)
                    return result;
            }
            foreach (var item in Interfaces.Where(item => item.Name == topClass))
            {
                result = item.FindTypeName(subClass);
                if (result != null)
                    return result;
            }
            foreach (var item in Structs.Where(item => item.Name == topClass))
            {
                result = item.FindTypeName(subClass);
                if (result != null)
                    return result;
            }
            foreach (var item in Enums.Where(item => item.Name == topClass))
            {
                result = item.FindTypeName(subClass);
                if (result != null)
                    return result;
            }
            return null;
        }

        public override CRenameItem FindTypeName(string typeName)
        {
            CRenameItem result = FindTypeNameDown(typeName);
            if (result != null)
                return result;
            return Parent != null ? ((CRenameItemClass)Parent).FindTypeName(typeName) : null;
        }
    }

    class CRenameItemClass : CRenameItemClassBase
    {
        public readonly CRenameItemClassBase InheritedStuff = new CRenameItemClassBase();

        public CodeClass2 GetElement()
        {
            return GetElement<CodeClass2>();
        }

        public override bool IdCollidesWithMember(string newName, string oldName)
        {
            return base.IdCollidesWithMember(newName, oldName) || InheritedStuff.IdCollidesWithMember(newName, oldName);
        }

        public override bool IdCollidesWithId(string newName, string oldName)
        {
            return base.IdCollidesWithId(newName, oldName) || InheritedStuff.IdCollidesWithId(newName, oldName);
        }

        public override CRenameItem FindTypeName(string typeName)
        {
            CRenameItem result = base.FindTypeName(typeName);
            if (result != null)
                return result;
            return InheritedStuff.FindTypeName(typeName);
        }
    }
}