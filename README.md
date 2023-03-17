# Inworld-Bannerlord-Mod
Inworld Bannerlord Mod Source Code and Portal Relayer

# [See documentation](https://bloctheworker.github.io/Inworld-Bannerlord-Mod/source/)

## Bannerlord Mod Code
Bannerlord mod code is written in C# and only works for Bannerlord. This part is not important if you are not into repacking your own mod for this game. It also contains some bad practices as well - because I had to move fast. So if you are new C# learner and/or seasoned dotnet wiz, don't bother checking this code because it might confuse or frusturate you 😄

## NodeJs Relayer
Nodejs relayer part is written in nodejs to have compatitiblity with Inworld Nodejs SDK. However, this version is slightly extended version. Currently SDK doesn't support generic character generations and/or run-time updates or deletion. With this relayer system I'm extending the capabilities of the Inworld Nodejs SDK. Yet, core communication/interaction logic is still relying on SDK.


