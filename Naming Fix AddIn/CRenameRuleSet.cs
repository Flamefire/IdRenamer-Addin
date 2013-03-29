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

namespace NamingFix
{
    enum ENamingStyle
    {
        UpperCamelCase,
        LowerCamelCase,
        UpperCase,
        LowerCase
    }

    struct SRenameRule
    {
        public bool DontChange;
        public String RemovePrefix;
        public String Prefix;
        public ENamingStyle NamingStyle;
    }

    class CRenameRuleSet
    {
        public const int Priv = 0;
        public const int Prot = 1;
        public const int Pub = 2;

        public SRenameRule
            Parameter,
            LokalVariable,
            LokalConst,
            Interface,
            Class,
            Enum,
            Struct;
        public SRenameRule[]
            Const,
            Field,
            Property,
            Method;

        public CRenameRuleSet()
        {
            Const = new SRenameRule[3];
            Field = new SRenameRule[3];
            Property = new SRenameRule[3];
            Method = new SRenameRule[3];
            Parameter.NamingStyle = ENamingStyle.LowerCamelCase;
            LokalVariable.NamingStyle = ENamingStyle.LowerCamelCase;
            LokalConst.NamingStyle = ENamingStyle.LowerCamelCase;
            Const[Priv].Prefix = "_";
            Field[Priv].Prefix = "_";
            Method[Priv].Prefix = "_";
            Property[Priv].Prefix = "_";
            Interface.Prefix = "I";
            Class.Prefix = "C";
            Enum.Prefix = "E";
            Struct.Prefix = "S";
        }
    }
}