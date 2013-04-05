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
    static class CUtils
    {
        public static void SplitTypeName(string className, out string topClass, out String subClass, bool first=true)
        {
            int p = (first) ? className.IndexOf('.') : className.LastIndexOf('.');
            if (p >= 0)
            {
                topClass = className.Substring(0, p);
                subClass = className.Substring(p + 1);
            }
            else
            {
                topClass = className;
                subClass = "";
            }
        }
    }
}