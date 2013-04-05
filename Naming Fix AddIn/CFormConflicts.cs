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
using System.Windows.Forms;

namespace NamingFix
{
    public partial class CFormConflicts : Form
    {
        public CFormConflicts()
        {
            InitializeComponent();
        }

        private void LbConflictsClick(object sender, EventArgs e)
        {
            int sel = lbConflicts.SelectedIndex;
            if (sel < 0)
                return;
            try
            {
                CNamingFix.Conflicts[sel].Show();
            }
            catch
            {
                MessageBox.Show("Item not found. Maybe it has been deleted, renamed are moved!", "Cannot show item", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}