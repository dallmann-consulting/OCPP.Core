/*
 * OCPP.Core - https://github.com/dallmann-consulting/OCPP.Core
 * Copyright (C) 2020-2026 dallmann consulting GmbH.
 * All Rights Reserved.
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using System.ComponentModel.DataAnnotations;

namespace OCPP.Core.Management.Models
{
    public class SetupModel
    {
        [Required(ErrorMessage = "FieldRequired")]
        public string Username { get; set; }

        [Required(ErrorMessage = "FieldRequired")]
        [MinLength(4, ErrorMessage = "FieldMinLength")]
        public string Password { get; set; }

        [Required(ErrorMessage = "FieldRequired")]
        [Compare(nameof(Password), ErrorMessage = "PasswordMismatch")]
        public string ConfirmPassword { get; set; }
    }
}
