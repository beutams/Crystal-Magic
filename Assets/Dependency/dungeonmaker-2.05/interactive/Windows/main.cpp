/*------------------------------------------------------------------------
main.cpp: demo program to run the DungeonMaker class on WINDOWS

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
#include <assert.h>
#include <stdlib.h>     //for rand()
#include <stdio.h>      //for file handling
#include <iostream.h>   //for console traffic
#include <time.h>


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
	default:cout << "Unexpected argument on command line, supported arguments are:\n
                -d <==> 'design'\n
                -+ <==> 'zoom in'\n
                -- <==> 'zoom out' (the last two are cumulative)" << endl;
	return(0);
	}
       ++argv;
       --argc;
     }

  Config config;
  if(!config.ReadDesign("design"))
    {
      cerr << "Could not read design file, aborting" << endl;
      return(0);
    }

  unsigned int seed = (unsigned) time( NULL );
  DungeonMaker theDungeonMaker(config , seed);

  SDL_Renderer Renderer;
  if(theDungeonMaker.ShowMovie() )
    {
      int scale = 4 + scaleModifier;
      if(scale < 2)
	scale = 2;
      if(!Renderer.Initialize(theDungeonMaker , scale))
	cerr << "could not initialize SDL_Renderer" << endl;
      if(!Renderer.RenderMap(theDungeonMaker , design) )
	cerr << "Could not render map)" << endl;
    }

  int counter , number;
  vector<alifegames::RectFill> des;
  unsigned int tT = 50;   //tickTime, makes movie speed in 1000/th of a second
  while(true)
    {
      if( theDungeonMaker.GetActiveGeneration() == theDungeonMaker.GetTunnelCrawlerGeneration() )
	{
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
	break;
    }

  if( (theDungeonMaker.GetTunnelCrawlerGeneration() < 0) || ( theDungeonMaker.GetActiveGeneration() < theDungeonMaker.GetTunnelCrawlerGeneration() ) )
    {
      theDungeonMaker.SeedCrawlersInTunnels();
      if(theDungeonMaker.ShowMovie() )   //seeding Crawlers changes the map too
	if(!Renderer.UpdateMap(theDungeonMaker.GetChangedThisIteration() , tT , design))
	  {
	    SDL_Quit();
	    goto quit;
	  }

      while(true)
	{
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
	    break;
	}
    }//end seeding Crawlers in Tunnels after normal run is finished

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
  vector<alifegames::RectFill>::iterator desIt;
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

  if(theDungeonMaker.StoreMovie() )
    {
      if(!theDungeonMaker.ShowMovie() )
	{//otherwise this is done earlier
	  if(!Renderer.Initialize(theDungeonMaker , 5))
	    cerr << "could not initialize SDL_Renderer" << endl;
	}
      if(!Renderer.ShowMovie( theDungeonMaker.GetMovie() , theDungeonMaker.GetDimX() , theDungeonMaker.GetDimY() , tT , CLOSED , design) )
	{
	  SDL_Quit();
	  goto quit;
	}
    }
  else if(!theDungeonMaker.ShowMovie() )  
    {//neither show nor store movie - show completed map when done
      if(!Renderer.Initialize(theDungeonMaker , 5))
	cerr << "could not initialize SDL_Renderer" << endl;
      if(!Renderer.RenderMap(theDungeonMaker , design) )
	cerr << "Could not render map)" << endl;
    }

  if(!Renderer.HangInThere())
    SDL_Quit();
 quit: ; //we quit this dungeon   

  return( 1 );
}
