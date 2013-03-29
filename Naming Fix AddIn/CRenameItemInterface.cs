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
    interface IRenameItemInterface
    {
        void AddFunc(CRenameItem func);
        void AddVar(CRenameItem var);
        void AddProperty(CRenameItem property);
        void AddClass(CRenameItem type);
        void AddInterface(CRenameItem type);
        void AddStruct(CRenameItem type);
        void AddEnum(CRenameItem type);

        /// <summary>
        ///     Checks if given Id collides with Member (Var/Property/Function) of this class and therfore is a invalid name
        /// </summary>
        /// <param name="newName"></param>
        /// <param name="oldName"></param>
        /// <returns></returns>
        bool IdCollidesWithMember(string newName, string oldName);

        /// <summary>
        ///     Checks if given Id collides with any other Id (Var/Property/Function) of this class and therefore is a invalide type name
        /// </summary>
        /// <param name="newName"></param>
        /// <param name="oldName"></param>
        /// <returns></returns>
        bool IdCollidesWithId(string newName, string oldName);
    }

    class CRenameItemInterfaceBase : CRenameItemType, IRenameItemInterface
    {
        public readonly List<CRenameFunction> Functions = new List<CRenameFunction>();
        public readonly List<CRenameItemVariable> Properties = new List<CRenameItemVariable>();

        public void AddFunc(CRenameItem func)
        {
            Functions.Add((CRenameFunction)func);
        }

        public void AddProperty(CRenameItem property)
        {
            Properties.Add((CRenameItemVariable)property);
        }

        public virtual void AddVar(CRenameItem var)
        {
            throw new NotImplementedException();
        }

        public virtual void AddClass(CRenameItem type)
        {
            throw new NotImplementedException();
        }

        public virtual void AddInterface(CRenameItem type)
        {
            throw new NotImplementedException();
        }

        public virtual void AddStruct(CRenameItem type)
        {
            throw new NotImplementedException();
        }

        public virtual void AddEnum(CRenameItem type)
        {
            throw new NotImplementedException();
        }

        public virtual bool IdCollidesWithMember(string newName, string oldName)
        {
            return Functions.Any(item => item.NewName == newName && item.Name != oldName) ||
                   Properties.Any(item => item.NewName == newName && item.Name != oldName);
        }

        public virtual bool IdCollidesWithId(string newName, string oldName)
        {
            return (NewName == newName && Name != oldName) || IdCollidesWithMember(newName, oldName) ||
                   (Parent != null && Parent.IdCollidesWithId(newName, oldName));
        }
    }

    class CRenameItemInterface : CRenameItemInterfaceBase
    {
        public CodeInterface2 GetElement()
        {
            return GetElement<CodeInterface2>();
        }
    }
}