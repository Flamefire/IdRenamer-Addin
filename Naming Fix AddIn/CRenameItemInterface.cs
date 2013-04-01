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
        public readonly CRenameItemList<CRenameMethod> Methods = new CRenameItemList<CRenameMethod>();
        public readonly CRenameItemList<CRenameItemProperty> Properties = new CRenameItemList<CRenameItemProperty>();

        public virtual void Add(CRenameItem item)
        {
            if (item is CRenameMethod)
                Methods.Add(item);
            else if (item is CRenameItemProperty)
                Properties.Add(item);
            else
                throw new NotImplementedException();
            item.Parent = this;
        }

        public virtual void CopyIds(CRenameItemInterfaceBase otherItem)
        {
            AddUniqueItems(Methods, otherItem.Methods, otherItem.ReadOnly);
            AddUniqueItems(Properties, otherItem.Properties, otherItem.ReadOnly);
        }

        protected void AddUniqueItems<T>(List<T> list, List<T> listOther, bool readOnly) where T : CRenameItem
        {
            foreach (T itemOther in listOther)
            {
                if (list.Any(item => item.Name == itemOther.Name))
                    continue;
                itemOther.ReadOnly = itemOther.ReadOnly || readOnly;
                list.Add(itemOther);
            }
        }

        public virtual bool IsMemberRenameValid(string newName, string oldName)
        {
            return Methods.IsRenameValid(newName, oldName) &&
                   Properties.IsRenameValid(newName, oldName);
        }

        public virtual bool IsIdRenameValid(string newName, string oldName)
        {
            return (NewName != newName || Name == oldName) &&
                   IsMemberRenameValid(newName, oldName) &&
                   (Parent == null || Parent.IsIdRenameValid(newName, oldName));
        }
    }

    class CRenameItemInterface : CRenameItemInterfaceBase
    {
        public CRenameItemInterfaceBase InheritedStuff=new CRenameItemInterfaceBase();
        public bool IsInheritedLoaded;

        public override bool ReadOnly
        {
            set { base.ReadOnly = value;
                InheritedStuff.ReadOnly = value;
            }
        }

        public CodeInterface2 GetElement()
        {
            return GetElement<CodeInterface2>();
        }

        public override void CopyIds(CRenameItemInterfaceBase otherItem)
        {
            InheritedStuff.CopyIds(otherItem);
            CRenameItemInterface otherItem2 = otherItem as CRenameItemInterface;
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
    }
}