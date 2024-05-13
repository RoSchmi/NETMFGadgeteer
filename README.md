# NETMFGadgeteer
Place to hold some NETMF and Gadgeteer Projects
1) CerberusDL40Programmer (by taylorza)
2) HeatingCurrentSurvey (Samples data from heating and Smartmeter)
3) Example to access Fritz!Dect with NETMF

Some hints to build the code:
The code for the old GHI-Modules can be build on Visual Studio 2013 or Visual Studio 2015.
1) To install the old Visual Studio 2013 it is now required to change some entries in the registry of your PC:
https://stackoverflow.com/questions/72031733/the-online-service-is-not-available-issue-in-visual-studio-professional-2013-w

Just add two DWORD values to the Windows Registry. Site go.microsoft.com now supports only TLS1.2 protocol.

[HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\.NETFramework\v4.0.30319]
"SystemDefaultTlsVersions"=dword:00000001
"SchUseStrongCrypto"=dword:00000001

2) On the used PC .NET Framework 3.5
has to be installed.

3) Install the following software: GHI Electronics NETMF SDK 2016 R1.exe; MS NETMF QFE2; .NET Micro Framework project system (netmfvs2013.vsix)
GHI FEZ Config 
https://docs.ghielectronics.com/software/netmf/downloads.html
Note: The NETMF Framework project system for Visual Studio 2013 will not install if Visual Studio 2022 is installed 
(VS 2022 must be deinstalled and reinstalled when the project system is installed)

4) To transfer the firmware on the old GHI Mainboards a PC with an old Windows Version has to be used (e.g. Windows 7) 
