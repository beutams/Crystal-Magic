/*------------------------------------------------------------------------
main.cpp: demo program to run the DungeonMaker class.

Copyright (C) 2001 Dr. Peter Henningsen

This program is released under the Free Software Foundation's General Public License (GPL). You cam also buy a commercial license. For further details, see the enclosed manual.

The GPL gives you the right to modify this software and incorporate it into your own project, as long as your project is also released under the Free Software Foundation's General Public License. You can obtain the text of this license by writing to 
Free Software Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA

For bug reports, and inquiries about including DungeonMaker code in projects that are not released under the GPL, contact:
peter@alifegames.com

For more information about DungeonMaker check out 
http://dungeonmaker.sourceforge.net
the mailing list there is open for your questions and support reqiests
------------------------------------------------------------------------*/
#include "DungeonMaker.h"
#include <cassert>
#include <cstdlib>     //for rand()
#include <cstdio>      //for file handling
#include <iostream>   //for console traffic


int main( int argc , char *argv[] )
{
  using namespace alifegames;

      DungeonMaker theDungeonMaker();
      theDungeonMaker.Init_fromFile("design", NULL);
	std::cout << "please stand by while we generate the dungeon..." << std::endl;
	  theDungeonMaker.generate();

      std::cout << "now plonking down stuff..." << std::endl;
      theDungeonMaker.PlonkDownStuff();
      /////////////////////////////////HERE ATTENTION !!
      theDungeonMaker.PutPlonkOnMap();
      //////////////////////////////////////////////////
///* ATTENTION: In this version, the method DungeonMaker:: PutPlonkOnMap() puts MOBs and treasure on the map literally, by changing the SquareData of the Map square where the stuff goes. This is just for demonstration purposes to make it easier to show stuff without having an engine for rendering objects. If you use the DungeonMaker in your own program, you must refrain from calling this function, and instead write your own function that puts stuff on the map as objects and leaves the MapData as it is.



      /////////////////TEST of Rooms std::vector
      //       std::cout << "We placed " << theDungeonMaker.NumberOfRooms() << " rooms in this dungeon (not counting design rooms)" << std::endl;
      //       if(theDungeonMaker.NumberOfRooms() > 0)
      //        	{
      // 	  theDungeonMaker.SortRooms();
      // 	  std::cout << "Room sizes are : ";
      // 	  for(unsigned int k = 0; k < theDungeonMaker.NumberOfRooms(); k++)
      // 	    std::cout << theDungeonMaker.RoomNumber(k).GetSize() << " - ";
      // 	  std::cout << std::endl;

      // 	  std::cout << " and room number zero has squares" << std::endl;
      // 	  Room room0 = theDungeonMaker.RoomNumber(0);
      // 	  std::vector<IntCoordinate> ins = room0.GetInside();
      // 	  for(int i = 0; i < ins.size(); i++)
      // 	    std::cout << "x=" << ins[i].first << "; y=" << ins[i].second << " - ";
      // 	  std::cout << std::endl;
      // 	}
      //////////////////Test end
      //the Dungeonmaker::Rooms-std::vector contains all rooms with their interior spaces - this will be used later for placemeent 
      //of treasure and MOBs

	SquareData dat = theDungeonMaker.GetMap(0,0);
	std::cout << "Cell 0,0 is of type " << dat << std::endl;
/*
  for(int x=0; x<theDungeonMaker.GetDimX(); x++)
  {
  	for(int y=0; y<theDungeonMaker.GetDimY(); y++)
  	{
		SquareData dat = theDungeonMaker.GetMap(x, y);
		std::cout << y << "\t" << x << "\t" << dat << std::endl;
  	}
  }
*/

  return( 1 );
}
