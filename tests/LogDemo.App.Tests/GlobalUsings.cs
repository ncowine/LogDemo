// The only place the two apps differ for the tests: the same classes live in different namespaces.
#if NETFRAMEWORK
global using LogDemo.App.NetFramework;
global using LogDemo.App.NetFramework.Commands;
global using LogDemo.App.NetFramework.Models;
global using LogDemo.App.NetFramework.Services;
global using LogDemo.App.NetFramework.ViewModels;
#else
global using LogDemo.App.Net;
global using LogDemo.App.Net.Commands;
global using LogDemo.App.Net.Models;
global using LogDemo.App.Net.Services;
global using LogDemo.App.Net.ViewModels;
#endif
