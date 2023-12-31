# Forged Alliance Forever - Lobby Sim reporting #

The game Forged Alliance (2007) was the last good RTS ever
before every developer started adding RPG elements and ruining
the genre irreparably forever (looking at you Dawn of War 2 and 3...).

The community mod Forged Alliance Forever (FAF) has kept the game
alive with improvements, servers and development. However since
the game is from 2007 barely anyone plays it. The few people who do
play the same two maps (Dual Gap and Seton's Clutch) almost exclusively.
This gives rise to the 'lobby sim' experience. Since there are so few
daily active users it can take between 10 minutes to an hour to fill
a custom game with about 8-12 players. In addition the community
is also extremely impatient so if a game is finally filled and not
started within the same 2 minutes everyone will leave again.

This means you need to constantly monitor the lobby to make sure you
can start it when it fills.

Rather than being tied to the computer waiting for a lobby to fill
what if you could monitor it via a mobile app and get notifications
when it was filled? This way you could carry out chores, read or
watch TV while waiting for the lobby.

While an API exists to monitor the active lobbies it requires an
OAuth client id and client secret. My initial designed involved
a mobile app where you login with the same login used for the game
and the app calls the APIs to monitor your active lobby.

This approach involves the overhead of getting a client id
and secret created for this use-case which I didn't want to do.
What if instead you ran a service on your desktop that monitored
your open lobby and reported to an API I own that I could then
use to build a monitoring web page or mobile app? That would,
as it turns out, be really stupid. So I built it.

Or rather, I started building it. I initially planned to use the
TestStack.White UI automation framework and just pull the data
I needed. However since the actually lobby process (not FAF Client)
is a game no UI elements except the window are available to the
automation library.

At this point I decided to use screenshots instead. On a repeating
interval the UI automation library takes a screenshot of the game.
The next step is obviously to use Tesseract to OCR the screenshot and
find which slots remain 'Open' versus either 'Closed' or occupied
by players. So - of course - I didn't do this because I didn't
want the Tesseract dependency.

I faffed around with OpenCV code for a while before deciding to
build my own heuristic based image processing. This is a mess
and it is incredibly bad code, but it works... some of the time.

The next step is to create a simple API that receives a payload of the type:

```
{
   "identifier": "flashy-ohio-23",
   "totalSlots": 12,
   "occupiedSlots": 4
}
```

This API stores the active connection with occupancy in memory. There
is a single page where you provide the identifier and it reports
the occupancy and history, maybe using server sent events or
websockets to keep the page updated.

I don't know if I'll ever get round to this.
