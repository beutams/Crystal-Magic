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
#include "SDL_Renderer.h"
#include <cassert>
#include <cstdlib>     //for rand()
#include <cstdio>      //for file handling
#include <iostream>   //for console traffic


int main( int argc , char *argv[] )
{
  using namespace alifegames;

  bool design = false;
  int scaleModifier = 0;
  while( (argc > 1) && ( '-' == argv[1][0] ) )
     {
       switch( argv[1][1] )
 	{
 	case('d'):    design = true;   break;
	case('+'):    scaleModifier++;   break;
	case('-'):    scaleModifier--;   break;
	default:std::cout << "Unexpected argument on command line, supported arguments are:\n"
                "-d <==> 'design'\n"
                "-+ <==> 'zoom in'\n"
                "-- <==> 'zoom out' (the last two are cumulative)" << std::endl;
	return(0);
	}
       ++argv;
       --argc;
     }
  
  std::vector< std::pair< char* , char* > > Files;

  char* designFile;
  char* Comment;

  designFile = "designEmpty";
  Comment = "CRAWLER DEMO # 1 (RandCrawlers Only)\n";
  std::pair<char* , char*> file8(designFile , Comment);
  Files.push_back( file8 );

  designFile = "designEmpty2";
  Comment = "CRAWLER DEMO # 2 (no RandCrawlers)\n";
  std::pair<char* , char*> file11(designFile , Comment);
  Files.push_back( file11 );

  designFile = "design7";
  Comment = "TUNNELER DEMO # 1\n";
  std::pair<char* , char*> file13(designFile , Comment);
  Files.push_back( file13 );

  designFile = "design6";
  Comment = "TUNNELER DEMO # 2\n";
  std::pair<char* , char*> file12(designFile , Comment);
  Files.push_back( file12 );

//  designFile = "design";
//   Comment = "TUNNELER DEMO\n";
//   std::pair<char* , char*> file0(designFile , Comment);
//   Files.push_back( file0 );

  designFile = "designOld1";
  Comment = "CHOKEPOINT IN LABYRINTH\n";
  std::pair<char* , char*> file6(designFile , Comment);
  Files.push_back( file6 );

  designFile = "design3A";
  Comment = "CHOKEPOINT IN DUNGEON\n ";
  std::pair<char* , char*> file5(designFile , Comment);
  Files.push_back( file5 );

//   designFile = "design2";
//   Comment = "LEFT HALF OF A 2-PLAYER PvP-MAP\n";
//   std::pair<char* , char*> file9(designFile , Comment);
//   Files.push_back( file9 );

  designFile = "design4A";
  Comment = "SMALL DUNGEON\n";
  std::pair<char* , char*> file1(designFile , Comment);
  Files.push_back( file1 );

  designFile = "design3WithTunnelers";
  Comment = "LABYRINTH/DUNGEON HYBRID\n";
  std::pair<char* , char*> file4(designFile , Comment);
  Files.push_back( file4 );

//   designFile = "design5";
//   Comment = "GREAT TREASURE\n"; //first check for available space
//   std::pair<char* , char*> file2(designFile , Comment);
//   Files.push_back( file2 );


  unsigned int designNumber = 0;
  bool userWantsToQuit = false;
  bool firstShowingOfThisDesign = true;

  //greet User:
  std::cout << 
           "WELCOME TO\n"
           "alifegames::DungeonMaker\n"
           "DEMO PROGRAM\n"
"We suggest that you open the MANUAL in your browser and read along.\n "
"The manual contains an introduction (which you may want to peruse for starters),\n"
"and further comments on all the designs you will see in the demo program.\n"
"You'll make best use of your time if you read the manual alongside running the program.\n" << std::endl;

  while(!userWantsToQuit)
    {
      Config config;
      //      if(!config.ReadDesign("design"))
      if(!config.ReadDesign( Files[designNumber].first ))
	{
	  std::cout << "Could not read design file, aborting" << std::endl;
	  return(0);
	}

      if(firstShowingOfThisDesign)
	std::cout << Files[designNumber].second;

      unsigned int seed;
      if(firstShowingOfThisDesign)
	{
	  seed = config.randSeed;   ///set random seed
	  std::cout << "using default random seed from design file, close SDL-window if you've seen it before" << std::endl;
	}
      else
	{
	  seed = (unsigned) time( NULL );
	  std::cout << "using system time random seed = " << seed << std::endl;
	}

      DungeonMaker theDungeonMaker(config , seed);
      firstShowingOfThisDesign = false;

      SDL_Renderer Renderer;
      if(theDungeonMaker.ShowMovie() )
	{
	  int scale = 4 + scaleModifier;
	  if(scale < 2)
	    scale = 2;
	  if(!Renderer.Initialize(theDungeonMaker , scale))
	    std::cout << "could not initialize SDL_Renderer" << std::endl;
	  if(!Renderer.RenderMap(theDungeonMaker , design) )
	    std::cout << "Could not render map)" << std::endl;
	}

      int counter , number;
      std::vector<alifegames::RectFill> des;
      unsigned int tT = 50;   //tickTime, makes movie speed in 1000/th of a second
      while(true)
	{
	  if(design)
	    std::cout << "...working on generation " << theDungeonMaker.GetActiveGeneration() << std::endl;

	  if( theDungeonMaker.GetActiveGeneration() == theDungeonMaker.GetTunnelCrawlerGeneration() )
	    {
	      //	      std::cout << "now seeding Crawlers in Tunnels... " << std::endl;
	      theDungeonMaker.SeedCrawlersInTunnels();
	      if(theDungeonMaker.ShowMovie() )   //seeding Crawlers changes the map too
		if(!Renderer.UpdateMap(theDungeonMaker.GetChangedThisIteration() , tT , design))
		  {
		    SDL_Quit();
		    goto quit;
		  }
	    }

	  while(theDungeonMaker.MakeIteration())
	    {
	      if(theDungeonMaker.ShowMovie() )
		if(!Renderer.UpdateMap(theDungeonMaker.GetChangedThisIteration() , tT , design))
		  {
		    SDL_Quit();
		    goto quit;
		  }
	    }
	  if( ! theDungeonMaker.AdvanceGeneration() )  //! there are Builders left
	    {
	      if(design)
		std::cout << "finished with Tunnelers ...   " << std::endl;
	      break;
	    }
	}

      if( (theDungeonMaker.GetTunnelCrawlerGeneration() < 0) || ( theDungeonMaker.GetActiveGeneration() < theDungeonMaker.GetTunnelCrawlerGeneration() ) )
	{
	  // std::cout << "now seeding Crawlers in Tunnels... " << std::endl;
	  theDungeonMaker.SeedCrawlersInTunnels();
	  if(theDungeonMaker.ShowMovie() )   //seeding Crawlers changes the map too
	    if(!Renderer.UpdateMap(theDungeonMaker.GetChangedThisIteration() , tT , design))
	      {
		SDL_Quit();
		goto quit;
	      }

	  while(true)
	    {
	      if(design)
		std::cout << "...working on generation " << theDungeonMaker.GetActiveGeneration() << std::endl;
	      while(theDungeonMaker.MakeIteration())
		{
		  if(theDungeonMaker.ShowMovie() )
		    if(!Renderer.UpdateMap(theDungeonMaker.GetChangedThisIteration() , tT , design))
		      {
			SDL_Quit();
			goto quit;
		      }
		}
	      if( ! theDungeonMaker.AdvanceGeneration() )  //! there are Builders left
		{
		  if(design)
		    std::cout << "finished with Crawlers inside Tunnels...   " << std::endl;
		  break;
		}
	    }
	}//end seeding Crawlers in Tunnels after normal run is finished

      if( theDungeonMaker.WantsMoreRoomsL() )
	std::cout << "stand by while creating rooms in labyrinth part...   " << std::endl;

      //first do this for the entire map if bg=OPEN
      if(OPEN == theDungeonMaker.GetBackground() )
	{
	  alifegames::RectFill rect(0 , 0 , theDungeonMaker.GetDimX() , theDungeonMaker.GetDimY() , theDungeonMaker.GetBackground() );
	  counter = 0;
	  number = theDungeonMaker.GetDimX() * theDungeonMaker.GetDimY();   //size of the square
	  while(theDungeonMaker.WantsMoreRoomsL() )
	    {
	      if( theDungeonMaker.CreateRoom(rect) )
		{//we have been successful
		  if(theDungeonMaker.ShowMovie() )
		    if(!Renderer.UpdateMap(theDungeonMaker.GetChangedThisIteration() , tT , design) )
		      {
			SDL_Quit();
			goto quit;
		      }
		}
	      else 
		counter++;
	      if(counter > number)
		break;
	    }
	}

      //now treat OPEN squares that were placed in the design:
    { std::vector<alifegames::RectFill>::iterator desIt;
      des = theDungeonMaker.GetDesign();
      for(desIt = des.begin(); desIt !=  des.end(); desIt++)
	{
	  alifegames::RectFill rect = *desIt;
	  if(rect.type != OPEN)
	    continue;   //we onlt make rooms in the labyrinth part

	  counter = 0;
	  number = (rect.endX - rect.startX) * (rect.endY - rect.startY);   //size of the square
	  while(theDungeonMaker.WantsMoreRoomsL() )
	    {
	      if( theDungeonMaker.CreateRoom(rect) )
		{//we have been successful
		  if(theDungeonMaker.ShowMovie() )
		    if(!Renderer.UpdateMap(theDungeonMaker.GetChangedThisIteration() , tT , design) )
		      {
			SDL_Quit();
			goto quit;
		      }
		}
	      else 
		counter++;
	      if(counter > number)
		break;
	    }
	}
      }

      //      std::cout << "now plonking down stuff..." << std::endl;
      theDungeonMaker.PlonkDownStuff();
      /////////////////////////////////HERE ATTENTION !!
      theDungeonMaker.PutPlonkOnMap();
      //////////////////////////////////////////////////
///* ATTENTION: In this version, the method DungeonMaker:: PutPlonkOnMap() puts MOBs and treasure on the map literally, by changing the SquareData of the Map square where the stuff goes. This is just for demonstration purposes to make it easier to show stuff without having an engine for rendering objects. If you use the DungeonMaker in your own program, you must refrain from calling this function, and instead write your own function that puts stuff on the map as objects and leaves the MapData as it is.
      if(theDungeonMaker.ShowMovie() )
	if(!Renderer.UpdateMap(theDungeonMaker.GetChangedThisIteration() , tT , design) )
	  {
	    SDL_Quit();
	    goto quit;
	  }

      std::cout << "finished constructing dungeon ...";

      if(theDungeonMaker.StoreMovie() )
	{
	  std::cout << "NOW SHOWING STORED MOVIE" << std::endl;
	  if(!theDungeonMaker.ShowMovie() )
	    {//otherwise this is done earlier
	      if(!Renderer.Initialize(theDungeonMaker , 5))
		std::cout << "could not initialize SDL_Renderer" << std::endl;
	    }
	  if(!Renderer.ShowMovie( theDungeonMaker.GetMovie() , theDungeonMaker.GetDimX() , theDungeonMaker.GetDimY() , tT , CLOSED , design) )
	    {
	      SDL_Quit();
	      goto quit;
	    }
	  std::cout << "finished showing stored movie ...";
	}
      else if(!theDungeonMaker.ShowMovie() )  
	{//neither show nor store movie - show completed map when done
	  std::cout << "now showing finished map ... ";
	  if(!Renderer.Initialize(theDungeonMaker , 5))
	    std::cout << "could not initialize SDL_Renderer" << std::endl;
	  if(!Renderer.RenderMap(theDungeonMaker , design) )
	    std::cout << "Could not render map)" << std::endl;
	}

      std::cout << " close SDL-window to continue ..." << std::endl;

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

      if(!Renderer.HangInThere())
	SDL_Quit();
    quit: ; //we quit this dungeon

      char c;
      std::cout << " 'q' to Quit " << std::endl << " 'n' for Next design " << std::endl << " 'r' (or anything) to Repeat this design with a different random seed " << std::endl << "Enter ==> ";
      std::cin >> c;
      if('q' == c)
	userWantsToQuit = true;
      else if('n' == c)
	{
	  designNumber++;
	  if(designNumber >= (Files.size()) )
	    {
	      designNumber = 0;
	      std::cout << "no more design files available, rolling over to first design file... " << std::endl;
	    }
	  firstShowingOfThisDesign = true;
	}
    }

  return( 1 );
}
