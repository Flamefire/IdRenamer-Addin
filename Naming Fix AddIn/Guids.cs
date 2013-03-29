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
    static class GuidList
    {
        public const string guidVariable_RenamerPkgString = "3d0ba8c5-842a-4c4c-9fb7-b2b562f18e7e";
        public const string guidVariable_RenamerCmdSetString = "508848dc-e39b-43ee-afc7-8500b661824a";

        public static readonly Guid guidVariable_RenamerCmdSet = new Guid(guidVariable_RenamerCmdSetString);
    };
}