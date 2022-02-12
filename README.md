# Enigma Encryption and Bombe Cracker: Some Playground Tools.

*_Visualize and explore how a mini version of the Bombe works_*

_Pete Wentworth_    mailto:cspwcspw@gmail.com  

## Overview

Understanding Enigma encryption is helped with nice tools.
There are plenty of simulators.  I like Dirk Rijmenants' one
from http://users.telenet.be/d.rijmenants/en/enigmasim.htm> Or
you might pefer the browser-based one at https://cryptii.com/pipes/enigma-machine.
Even build the fun paper version described at http://wiki.franklinheath.co.uk/index.php/Enigma/Paper_Enigma

So here we have Yet Another Collection of Software Tools for 
the Enigma and Bombe (YACSTEB???)

Of course I started with my own Enigma machine simulator. 
And some tools for identifying cycles / building graphs, etc.

But the bit I found most difficult to wrap my head around was how the 
Bombe machine helped crack intercepted messages.  So the main
contribution here is the Mini-Bombe application: a small 8-symbol Bombe 
to help deepen insight into why Turing's original Bombe could filter
out wheel settings that led to contradictions. That's the first step.

But then, why did Welchman's subsequent addition of the Diagonal Board 
greatly improve the Bombe's ability to reject impossible rotor
wheel settings.  This helped reduce the 
"false stop situations", each of which had to be further explored by hand.

You should first read Ellsbury's stuff in detail.  Particularly,
http://www.ellsbury.com/bombe3.htm where he describes how a small
8-symbol Bombe would work.  This was my starting point.

## The Mini-Bombe

I'm not going to cover much groundwork again. But here are the most
important "lightbulb" insights for me:




