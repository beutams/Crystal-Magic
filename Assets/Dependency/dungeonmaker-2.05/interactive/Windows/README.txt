This program is distributed under the Free Software Foundation's General Public License.
Commercial licenses are also available. Copyright for program and documentation: Dr. Peter Henningsen, 2002

People who have tried to run earlier versions of the DungeonMaker on Windows
had problems, which may be rooted in the fact that the
DungeonMaker-demo-program needs access to a command line and to an SDL-window
simutaneously. To simplify things for Wondows people, 
this folder contains a different version of the
main()-program which is not interactive. It just creates one dungeon, and
closes when you close the SDL-window. In order to see different dungeons, you
must manually rename the various design files to "design".

This program uses the SDL-devel library, which is available from 
http://libsdl.org
In order to compile the program, read the documentation on that website.
Things are different depending on the compiler you use.
If you build a Windows executable, we would like to distribute it on SourceForge.
So please let us know.

Command line parameters are described in the README.txt file in the "dungeonmaker2" folder.


Peter Henningsen
peter@alifegames.com
