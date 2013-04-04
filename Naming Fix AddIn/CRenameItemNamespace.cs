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
using System.Linq;

namespace NamingFix
{
    class CRenameItemNamespace : CRenameItemElement, IRenameItemContainer
    {
        public readonly CRenameItemList<CRenameItemClass> Classes = new CRenameItemList<CRenameItemClass>();
        public readonly CRenameItemList<CRenameItemInterface> Interfaces = new CRenameItemList<CRenameItemInterface>();
        public readonly CRenameItemList<CRenameItemNamespace> Namespaces = new CRenameItemList<CRenameItemNamespace>();
        public readonly CRenameItemList<CRenameItemType> Types = new CRenameItemList<CRenameItemType>();

        public override CodeElement Element
        {
            set { _InternalElement = value; }
        }

        public override bool IsSystem
        {
            set
            {
                base.IsSystem = value;
                Types.ForEach(item => item.IsSystem = value);
                Namespaces.ForEach(item => item.IsSystem = value);
                Classes.ForEach(item => item.IsSystem = value);
                Interfaces.ForEach(item => item.IsSystem = value);
            }
        }

        public override bool IsRenameValid()
        {
            //Renaming if namespaces is currently not supported!
            return (Name == NewName);
        }

        public void Add(CRenameItem item)
        {
            if (item is CRenameItemClass)
                Classes.Add(item);
            else if (item is CRenameItemInterface)
                Interfaces.Add(item);
            else if (item is CRenameItemType)
                Types.Add(item);
            else if (item is CRenameItemNamespace)
                Namespaces.Add(item);
            else
                throw new ArgumentException();
            item.Parent = this;
            item.IsSystem = IsSystem;
        }

        public bool IsConflictLocVar(string newName, string oldName)
        {
            return Classes.Any(item => item.IsConflictLocVar(newName, oldName));
        }

        public bool IsConflictType(string newName, string oldName)
        {
            return Classes.IsConflict(newName, oldName) ||
                   Interfaces.IsConflict(newName, oldName) ||
                   Namespaces.IsConflict(newName, oldName) ||
                   Parent != null && Parent.IsConflictType(newName, oldName);
        }

        public bool IsConflictId(string newName, string oldName)
        {
            //No ids, so no conflicts
            return false;
        }

        private CRenameItem FindTypeNameDown(String typeName)
        {
            string mainType, subType;
            CUtils.SplitTypeName(typeName, out mainType, out subType);

            CRenameItemClass itemClass = Classes.Find(mainType);
            if (subType != "")
            {
                //Only classes and namespaces can have subTypes so either get down or exit
                if (itemClass != null)
                    return itemClass.FindTypeNameDown(subType);
                CRenameItemNamespace ns = Namespaces.Find(mainType);
                return ns != null ? ns.FindTypeNameDown(subType) : null;
            }
            if (itemClass != null)
                return itemClass;
            CRenameItem result = Interfaces.Find(mainType);
            return result ?? Types.Find(mainType);
        }

        public CRenameItem FindTypeByName(string typeName)
        {
            CRenameItem result = FindTypeNameDown(typeName);
            if (result != null)
                return result;
            return Parent != null ? Parent.FindTypeByName(typeName) : null;
        }

        public CRenameItemNamespace AddOrGetNamespace(string name, CodeElement element)
        {
            string mainType, subType;
            CUtils.SplitTypeName(name, out mainType, out subType);
            CRenameItemNamespace ns = Namespaces.Find(mainType);
            if (ns == null)
            {
                ns = new CRenameItemNamespace {Element = element, Name = mainType};
                Add(ns);
            }
            if (subType != "")
                return ns.AddOrGetNamespace(subType, element);
            ns.Element = element;
            return ns;
        }
    }
}