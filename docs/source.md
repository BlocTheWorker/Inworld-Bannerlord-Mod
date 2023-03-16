# Source Code Explanation

If you are asking `Why the hell am I reading this?` to yourself, it means this section isn't for you. Please check installation or index page.

## Understanding the Different Parts

As you might guess, using Inworld directly in Bannerlord is not possible due to certain reasons. Currently mod works with application that gets ran

### Bannerlord Mod Code

Bannerlord mod code is written in C# and only works for Bannerlord. This part is not important if you are not into repacking your own mod for this game. It also contains some `bad practices` as well - because I had to move fast. So if you are new C# learner and/or seasoned dotnet wiz, don't bother checking this code because it might confuse or frusturate you 😄

### NodeJs Relayer

Nodejs relayer part is written in nodejs to have compatitiblity with Inworld Nodejs SDK. However, this version is slightly extended version. Currently SDK doesn't support generic character generations and/or run-time updates or deletion. With this relayer system I'm extending the capabilities of the Inworld Nodejs SDK. Yet, core communication/interaction logic is still relying on SDK.

#### How?

I'm simply using the same functions as portal. What I'm doing is, I'm logging in to firebase as our user. This part isn't even directly related to Inworld, since it's using googleapis. This call gives you login idtoken and refresh token. From there we are securing token (again from googleapis) to get actual access_token and refresh_token. I'm doing basic token caching based on it's TTL, and I'm not re-logging with password/email combination. And rest is just mimicking what portal is doing.


## How to run?

- Bannerlord: You can use sln for building for Bannerlord, well, if you have Bannerlord installed, of course.
- Nodejs: You need install required things such as node, and then it's same as any other node app. `npm i` and `node Client` starts the server locally.
