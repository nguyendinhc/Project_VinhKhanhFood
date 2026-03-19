using System;
using System.Collections.Generic;

namespace VinhKhanhApi.Models;

public partial class Role
{
    public int RoleId { get; set; }

    public string RoleName { get; set; } = null!;

    public virtual ICollection<AdminUser> AdminUsers { get; set; } = new List<AdminUser>();
}
