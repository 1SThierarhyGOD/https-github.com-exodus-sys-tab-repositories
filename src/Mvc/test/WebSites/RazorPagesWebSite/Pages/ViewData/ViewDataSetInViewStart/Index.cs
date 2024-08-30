// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RazorPagesWebSite.ViewDataSetInViewStart;

public class Index : PageModel
{
    [ViewData]
    public string ValueFromPageModel => "Value from Page Model";
}
