# Inworld Mod

Welcome to Mount&Blade II: Bannerlord's Inworld - Calradia mod page. In this page, you can find information about how to download the mod and how to install it.

As well as, for curious developers, I left the source code as well.

## Download

Currently mod is only available in [nexusmods](https://www.nexusmods.com/mountandblade2bannerlord/mods/5273https:/)

#### [Download the mod from here](https://www.nexusmods.com/mountandblade2bannerlord/mods/5273https:/)

!!! Danger "Beware"
    Please always respect to source and download mods from source to minimize any malware on your side.

## About

> Ever wanted to talk with villagers, commoners, lords, and ladies in the realm, listen to their dreams, ideas, plans? Get ready to embark on an epic adventure like no other! Inworld Calradia mod takes the Bannerlord experience to the next level by allowing you to engage with a diverse range of characters within the realm. From humble villagers to powerful lords and ladies, you can now immerse yourself in their hopes, dreams, and plans, using the latest technology.

> With the powerful GPT AI and Text-to-speech features from Inworld AI, you'll feel like you're really there, speaking with characters that are unique and authentic to the game. Each noble in Calradia has been created with a range of different backgrounds and traits, ensuring that every conversation you have will be truly one-of-a-kind. Through your expert conversational skills, you'll unlock secrets and learn about hidden ambitions and plots. And with the ability to chat with villagers and townsfolk, you can gain valuable insights into the daily lives of the people in Calradia.

> Every noble in the Calradia will be generated based on their in game characters but with variety of possible background combinations, this allows you to understand their characteristics, but as well as makes your each gameplay unique. These characters persist throughout your save game. You will be able to chat with them, hear their voice, almost instantly the moment you enter your input. You will be able to learn way more than you can imagine, uncover hidden ambitions and plots just by your talking skills. You can also talk with villagers in village, townsfolk in towns and taverns to gather information or just to immerse yourself with a small chit chat with the people, to understand how they are feeling.

> So don't wait any longer - dive into the immersive world of Inworld Calradia mod and discover a whole new level of excitement in Bannerlord!

## How does it work?
Under the hood, it works with communicating with Inworld servers. Inworld provides SDK to communicate and talk with characters already but it's not providing any tools to generate characters programmatically and update them. And this is exactly what we want for Sandbox experience. So this mod is coming as two parts, one executable file and one mod system. Executable file is communicating with Inworld's servers and your account (see Installation guide)

Aside from handling the conversation and in-game logic, mod also responsible with checking the characters in the scene and generating them if they don't exist. It is achieving this with using a Story Engine that uses a large corpus of text to generate character on top of the already vast information Inworld's GPT has. Mod generates character by respecting their role and attributes in the Calradia, meaning that a regular clan member will be different from a clan ruler or faction ruler or if a person is a battle-like person etc, you get different characters generated. Characters get a lot of information and gets generated in this way. Also, voices are getting assigned in this generation phase as well. There are certain dynamic information I'm passing the characters when you go into scene. 

For nobles, generation happens only once, and only when you first enter a scene with them (castle or town keep) and you shouldn't take any actions until you see this text otherwise you cannot communicate with them
 
Once you see this, it means characters are ready to go.

For townfolk/villagers, system is a little different, unlike nobles, you cannot talk with same townfolk with same personality twice in this version. This is, unfortunately a limitation I had to put to avoid thousands of villager generation and causing  a mess in your account and in Inworld's account. That being said, they are way much faster to talk with. Meaning that you can go to any town and start talking with any one in there in seconds. Moment you start the regular chat, it takes ~4 seconds to create character for this peasant/common folk. And from that moment on you can continue talk with them. Unlike nobles, these people have jobs and they are mostly in hurry. They also have more variant on their personality, meaning that some villagers can talk very blunt while some talk bluntly or some talk formally. These are small nuances that you can pick up during your conversation. 

## How to have conversation?
This mod doesn't remove Bannerlord's default conversation settings in favor of gameplay, instead, I allow user to engage in textual discussion directly upon their request. Just say "I need to talk with you" and you will see the textbox. Existing this view is easy. You can simply press TAB button to exit the textual conversation style once you are finished.

## What do characters know?
They know practically anything they should know.
For nobles, they know, who are they, how old are they, who is their father, mother, brothers, sisters, children if they have any. Furthermore, they know which clan they are in, what other clans exist in their faction, different war/peace states with other factions. They know rulers of factions, cultures in Calradia, tales in calradia and such. They also know their past with you, such as, if your companion/clan member is killed by them in battle, they will remember this , or if you killed any of their family members, they will remind you that. They also remember past battles they had with you, and they might refer to this. Aside from that, they know past battles happened in Calradia. They also know overall geography of Calradia. The town they are currently located at. They know the ruler of that town. They have unique ambitions or plans or interests. They know rumors about other clans which they might or might not tell to you. They also have unique background stories generated for them that makes each gameplay with different generation is unique.
For common people, it's almost similar to nobles but of course they don't have any knowledge on family and clans. Rest of the things are identical with nobles.
All characters "see" what you are wearing and what they are wearing. They also know where they are right now (ie. if they are in tavern, they know that they are in tavern) They also know parties nearby (if there are any)

## Important - Read this before use
- This mod is using Inworld's existing endpoints and SDK's. This means you NEED an Inworld account. Follow the guide on how to install the mod and setup your environment.
- Mod only works for English language. Even though, AI sometimes answers in different language when you ask question in that language or ask it to reply in that language (such as Polish, Turkish etc) this isn't normal behavior. Usually you will get answer like "I don't speak your language" and this is expected. Inworld is working hard to bring new languages to their system and once they do this, you should be able to chat in your langauge. (TTS works very poorly in other languages) 
Inworld is applying User Guidelines to your messages. This means, you can get banned if you things that are violating that. You won't get banned straight away, but you might, keep your conversations in human-level please. Unlike Chatgpt, AI might still respond to you in this instance, however your account can get into trouble.  
- Inworld is constantly improving and updating their systems. This is great, but also means that you can see some behaviors that is not great for your taste. If you encounter weird behavior, don't report it to me - instead please go to Inworld's Discord channel﻿ and describe your behavior. 
- This mod gives no guarantee to work with other mods and it's not aiming to work with other mods either. This especially includes mods like external Harmony and other stuff. This mod does NOT require any extra mods to run. It should work just fine for any mod that is not total conversion mod. But no guarantee. If you face issues with it, just post it to posts section, don't open bug report. 
- Mod can stop working due to several reasons such as Inworld changing the backend or Taleworlds making an update. While I will try my best to keep up with these, I cannot provide any guarantee. 
- Even though currently system for memory is in place within the mod, it's not ready on Inworld's side. Meaning that, you are unable to continue conversation after returning it to other day. All the information you give to AI in previous session will be lost. Inworld will bring memory option internally soon, so you won't have this issue in the future.

## Background Application
You will notice that once you launch the game with the mod, it will launch another executable similar to this
It might be scary, but don't worry, this is perfectly normal as stated in there. Once you close the game, you can manually close this one too.

## Helper Cheats
Mod designed to use your Inworld workspace and can be used one-save game only. If you want to refresh everything or just delete everything, press ALT+~ key and it will open the console. Type inworld. and you will see the options. Select one of them and wait 1-2 seconds.

## Reporting Bugs
Don't report bugs to here but rather use Nexusmods' bug section. Also carefully read the bug reporting guidelines if you want to get resolution.


## Installation and Setup

Please visit [installation](installation.md) page for extensive information about how to instal and setup the mod
