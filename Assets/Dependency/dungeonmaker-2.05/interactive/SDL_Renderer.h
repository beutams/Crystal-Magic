/*------------------------------------------------------------------------
DungeonMaker.cpp: implementation of the DungeonMaker class.

Copyright (C) 2001 Dr. Peter Henningsen

This set of classes is released under the Free Software Foundation's General Public License (GPL). You cam also buy a commercial license. For further details, see the enclosed manual.

The GPL gives you the right to modify this software and incorporate it into your own project, as long as your project is also released under the Free Software Foundation's General Public License. You can obtain the text of this license by writing to 
Free Software Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA

For bug reports, support requests, and inquiries about including DungeonMaker code in projects that are not released under the GPL, contact:
peter@alifegames.com

For more information about DungeonMaker check out 
http://dungeonmaker.sourceforge.net
------------------------------------------------------------------------*/
#ifndef SDL_RENDERER_H_INCLUDED
#define SDL_RENDERER_H_INCLUDED 1

#include <SDL/SDL.h>
#include "DungeonMaker.h"

class SDL_Renderer
{
 private:
  SDL_Surface* screen;
  SDL_Surface* buffer;
  int scale;   //one square is scale * scale pixels
  SDL_PixelFormat* fmt;
  Uint32 time;

 public:
  SDL_Renderer() {}
  bool Initialize(alifegames::DungeonMaker& tDM , int sc);  //returns true upon success, false on failure
  Uint32 GetColor( alifegames::SquareData dat , bool d);
  bool RenderMap( alifegames::DungeonMaker& tDM , bool d);
  //  bool UpdateMap(alifegames::DungeonMaker& tDM);
  bool UpdateMap(std::vector< alifegames::SquareInfo > ChangedSquares , unsigned int tickTime , bool d);
  bool ShowMovie(std::vector< std::vector< alifegames::SquareInfo > > movie , int dX , int dY , unsigned int tickTime , alifegames::SquareData bg , bool d);

  bool HangInThere();
};

#endif // SDL_RENDERER_H_INCLUDED

/*      //new for rendering in SDL */
	  
/*       SDL_Surface* screen; */
/*       SDL_Surface* buffer; */
/*       SDL_Surface* staticMap; */
/*       SDL_Rect src , dest; */
/*       int scale = 5; */

/*       if(SDL_Init(SDL_INIT_VIDEO) != 0) */
/* 	{ */
/* 	  printf("Unable to init SDL: %s\n" , SDL_GetError()); */
/* 	  return(1); */
/* 	} */
/*       atexit(SDL_Quit); */
/*       screen = SDL_SetVideoMode(scale * x , scale * y , 16 , 0); */
/*       if(screen == NULL) */
/* 	{ */
/* 	  printf("Unable to set video mode: %s\n" , SDL_GetError()); */
/* 	  return(1); */
/* 	} */

/*       //set buffer and staticMap to the same format as screen */
/*       SDL_PixelFormat* fmt; */
/*       fmt = screen -> format; */
/*       buffer = SDL_CreateRGBSurface(SDL_SRCCOLORKEY ,scale * x , scale * y , 16 , fmt -> Rmask , fmt -> Gmask , fmt -> Bmask ,  fmt -> Amask); */
/*       if(buffer == NULL) */
/* 	{ */
/* 	  printf("Unable to allocate video memory: %s\n" , SDL_GetError()); */
/* 	  return(1); */
/* 	} */

/*       staticMap = SDL_CreateRGBSurface(SDL_SRCCOLORKEY ,scale * x , scale * y , 16 , fmt -> Rmask , fmt -> Gmask , fmt -> Bmask ,  fmt -> Amask); */
/*       if(staticMap == NULL) */
/* 	{ */
/* 	  printf("Unable to allocate video memory: %s\n" , SDL_GetError()); */
/* 	  return(1); */
/* 	} */


/*       SDL_Surface* wall = SDL_LoadBMP( "pics/Black.bmp" ); */
/*       if(wall == NULL) */
/* 	{ */
/* 	  printf("Unable to load Black.bmp\n"); */
/* 	  return(1); */
/* 	} */
/*       src.x = 0; */
/*       src.y = 0; */
/*       src.w = wall->w; */
/*       src.h = wall->h; */
/*       dest.w = wall->w; */
/*       dest.h = wall->h; */

/*       assert(src.w == scale);  //that's how it's gotta be, if you use different size graphics, change the scale */

/*       SDL_Surface* column = SDL_LoadBMP( "pics/Column.bmp" ); */
/*       if(column == NULL) */
/* 	{ */
/* 	  printf("Unable to load Column.bmp\n"); */
/* 	  return(1); */
/* 	} */
/*       SDL_Surface* nojoin = SDL_LoadBMP( "pics/Nojoin.bmp" ); */
/*       if(nojoin == NULL) */
/* 	{ */
/* 	  printf("Unable to load Nojoin.bmp\n"); */
/* 	  return(1); */
/* 	} */

/*       //for some arcane reason, Up and Left, and Down and Right are mixed up and have been switched to get the correct results. */
/*       //I have no idea why this is so. So much for pausing a project for half a year... */

/*       SDL_Surface* up = SDL_LoadBMP( "pics/Left.bmp" );    */
/*       if(up == NULL) */
/* 	{ */
/* 	  printf("Unable to load Left.bmp\n"); */
/* 	  return(1); */
/* 	} */
/*       SDL_SetColorKey( up , SDL_SRCCOLORKEY , SDL_MapRGB( up->format , 255 , 255 , 255 ) ); */

/*       SDL_Surface* down = SDL_LoadBMP( "pics/Right.bmp" ); */
/*       if(down == NULL) */
/* 	{ */
/* 	  printf("Unable to load Right.bmp\n"); */
/* 	  return(1); */
/* 	} */
/*       SDL_SetColorKey( down , SDL_SRCCOLORKEY , SDL_MapRGB( down->format , 255 , 255 , 255 ) ); */

/*       SDL_Surface* left = SDL_LoadBMP( "pics/Up.bmp" ); */
/*       if(left == NULL) */
/* 	{ */
/* 	  printf("Unable to load Up.bmp\n"); */
/* 	  return(1); */
/* 	} */
/*       SDL_SetColorKey( left , SDL_SRCCOLORKEY , SDL_MapRGB( left->format , 255 , 255 , 255 ) ); */

/*       SDL_Surface* right = SDL_LoadBMP( "pics/Down.bmp" ); */
/*       if(right == NULL) */
/* 	{ */
/* 	  printf("Unable to load Down.bmp\n"); */
/* 	  return(1); */
/* 	} */
/*       SDL_SetColorKey( right , SDL_SRCCOLORKEY , SDL_MapRGB( right->format , 255 , 255 , 255 ) ); */

/*       SDL_Surface* leftact = SDL_LoadBMP( "pics/UpActive.bmp" ); */
/*       if(leftact == NULL) */
/* 	{ */
/* 	  printf("Unable to load UpActive.bmp\n"); */
/* 	  return(1); */
/* 	} */
/*       SDL_SetColorKey( leftact , SDL_SRCCOLORKEY , SDL_MapRGB( leftact->format , 255 , 255 , 255 ) ); */

/*       SDL_Surface* rightact = SDL_LoadBMP( "pics/DownActive.bmp" ); */
/*       if(rightact == NULL) */
/* 	{ */
/* 	  printf("Unable to load DownActive.bmp\n"); */
/* 	  return(1); */
/* 	} */
/*       SDL_SetColorKey( rightact , SDL_SRCCOLORKEY , SDL_MapRGB( rightact->format , 255 , 255 , 255 ) ); */

/*       SDL_Surface* upact = SDL_LoadBMP( "pics/leftActive.bmp" ); */
/*       if(upact == NULL) */
/* 	{ */
/* 	  printf("Unable to load leftActive.bmp\n"); */
/* 	  return(1); */
/* 	} */
/*       SDL_SetColorKey( upact , SDL_SRCCOLORKEY , SDL_MapRGB( upact->format , 255 , 255 , 255 ) ); */

/*       SDL_Surface* downact = SDL_LoadBMP( "pics/RightActive.bmp" ); */
/*       if(downact == NULL) */
/* 	{ */
/* 	  printf("Unable to load RightActive.bmp\n"); */
/* 	  return(1); */
/* 	} */
/*       SDL_SetColorKey( downact , SDL_SRCCOLORKEY , SDL_MapRGB( downact->format , 255 , 255 , 255 ) ); */

/*       SDL_Surface* jumper = SDL_LoadBMP( "pics/Jumper.bmp" ); */
/*       if(jumper == NULL) */
/* 	{ */
/* 	  printf("Unable to load Jumper.bmp\n"); */
/* 	  return(1); */
/* 	} */
/*       SDL_SetColorKey( jumper , SDL_SRCCOLORKEY , SDL_MapRGB( jumper->format , 255 , 255 , 255 ) ); */

/*       //set map to white */
/*       SDL_FillRect( staticMap , NULL , SDL_MapRGB( fmt , 255 , 255 , 255 ) ); */

/*       //first show the initial design elements: */
/*       pDM -> UpdateAddedToMapThisTurn(); */

/*       std::vector<SquareInfo> latestWalls = pDM -> ShowNewWalls( ); */
/*       std::vector<SquareInfo>::iterator i; */
/*       for(i = latestWalls.begin(); i != latestWalls.end(); ++i) */
/* 	{ */
/* 	  dest.x = scale * ( (*i).ShowX() ); */
/* 	  dest.y = scale * ( (*i).ShowY() ); */
/* 	  switch((*i).ShowType() ) */
/* 	    { */
/* 	    case( WALLGEN0 ):  */
/* 	    case( WALLGEN1 ):  */
/* 	    case( WALLGEN2 ):  */
/* 	      SDL_BlitSurface(wall , &src , staticMap , &dest);  break; */
/* 	    case( COLUMN ): */
/* 	      SDL_BlitSurface(column , &src , staticMap , &dest);  break;  */
/* 	    case( NOJOINWALL ):  */
/* 	      SDL_BlitSurface(nojoin , &src , staticMap , &dest);  break;  */
/* 	    default: assert(0); */
/* 	    } */
/* 	} */
/*       //      SDL_BlitSurface(staticMap , NULL , screen , NULL); */

/*       SDL_BlitSurface(staticMap , NULL , buffer , NULL); */
/*       //now we put the Crawlers and Jumpers on the buffer */
/*       pDM ->  UpdateAllBuildersThisTurn( ); */

/*       std::vector<SquareInfo> currentBuilders = pDM -> ShowBuilders( ); */
/*       for(i = currentBuilders.begin(); i !=  currentBuilders.end(); ++i) */
/* 	{ */
/* 	  dest.x = scale * ( (*i).ShowX() ); */
/* 	  dest.y = scale * ( (*i).ShowY() ); */
/* 	  switch((*i).ShowType() ) */
/* 	    { */
/* 	    case( CRAWLERUP ):  */
/* 	      SDL_BlitSurface(upact , &src , buffer , &dest);  break; */
/* 	    case( CRAWLERDOWN ):  */
/* 	      SDL_BlitSurface(downact , &src , buffer , &dest);  break; */
/* 	    case( CRAWLERLEFT ):  */
/* 	      SDL_BlitSurface(leftact , &src , buffer , &dest);  break; */
/* 	    case( CRAWLERRIGHT ):  */
/* 	      SDL_BlitSurface(rightact , &src , buffer , &dest);  break; */
/* 	    case( DORMANTCRAWLERUP ):  */
/* 	      SDL_BlitSurface(up , &src , buffer , &dest);  break; */
/* 	    case( DORMANTCRAWLERDOWN ):  */
/* 	      SDL_BlitSurface(down , &src , buffer , &dest);  break; */
/* 	    case( DORMANTCRAWLERLEFT ):  */
/* 	      SDL_BlitSurface(left , &src , buffer , &dest);  break; */
/* 	    case( DORMANTCRAWLERRIGHT ):  */
/* 	      SDL_BlitSurface(right , &src , buffer , &dest);  break; */
/* 	    case( JUMPERUP ):  */
/* 	    case( JUMPERDOWN ):  */
/* 	    case( JUMPERLEFT ):  */
/* 	    case( JUMPERRIGHT ):  */
/* 	      SDL_BlitSurface(jumper , &src , buffer , &dest);  break; */

/* 	    default: assert(0); */
/* 	    } */
/* 	} */

/*       //now blit the buffer on the screen */
/*       SDL_BlitSurface(buffer , NULL , screen , NULL); */

/*       SDL_UpdateRect(screen , 0 , 0 , 0 , 0 ); */

/*       SDL_Delay(3000);   //pause to see the design elements */

/*       //now go through the iteration */
/*       bool thereAreBuilders = true;  //we just assume that's the case, if not the iteration stops immediately anyway */
/*       SDL_Event event; */

/*       unsigned int iteration = 0; */
/*       while(thereAreBuilders) */
/* 	{ */
/* 	  while(SDL_PollEvent(&event) != 0) */
/* 	    { */
/* 	      switch(event.type) */
/* 		{ */
/* 		case (SDL_QUIT):	       */
/* 		  SDL_FreeSurface(staticMap); */
/* 		  SDL_FreeSurface(buffer); */
/* 		  SDL_FreeSurface(wall); */
/* 		  SDL_FreeSurface(column); */
/* 		  SDL_FreeSurface(nojoin); */
/* 		  SDL_FreeSurface(up); */
/* 		  SDL_FreeSurface(down); */
/* 		  SDL_FreeSurface(left); */
/* 		  SDL_FreeSurface(right); */
/* 		  SDL_FreeSurface(leftact); */
/* 		  SDL_FreeSurface(rightact); */
/* 		  SDL_FreeSurface(upact); */
/* 		  SDL_FreeSurface(downact); */
/* 		  SDL_FreeSurface(jumper); */

/* 		  goto jump; */
/* 		} */
/* 	    } */

/* 	  //go through the dungeon construction process one step at a time, then render */
/* 	  unsigned int numBuild = pDM -> MakeMapStep(); //one small step for man */
/* 	  if( 0 == numBuild )  */
/* 	    thereAreBuilders = false; */

/* 	  unsigned int numAct = pDM -> UpdateAllBuildersThisTurn( ); */

/* 	  if(0 == iteration%99) */
/* 	    cout << "Starting generation number " << (iteration/99) << endl; */

/* 	  iteration++; */

/* 	  //the following is good for debugging builders, there's a bug with the generation code that I will not hunt down */
/*           //because that code will be replaced in the new version */
/* 	  //	  cout << "Iteration " << iteration << " - Builders total = " << numBuild << "; active = " << numAct << endl; */

/* 	  if( 0 == numAct ) */
/* 	    { //''thereAre-No-ActiveBuilders */
/* 	      continue; */
/* 	    } */

/* 	  if( pDM -> UpdateAddedToMapThisTurn() ) */
/* 	    { */
/* 	      //the map has changed */
/* 	      //	      showThisIteration = true; */
/* 	      latestWalls = pDM -> ShowNewWalls( ); */
/* 	      //	      std::vector<SquareInfo>::iterator i;   //easy to forget */
/* 	      for(i = latestWalls.begin(); i != latestWalls.end(); ++i) */
/* 		{ */
/* 		  dest.x = scale * ( (*i).ShowX() ); */
/* 		  dest.y = scale * ( (*i).ShowY() ); */
/* 		  switch((*i).ShowType() ) */
/* 		    { */
/* 		    case( WALLGEN0 ):  */
/* 		    case( WALLGEN1 ):  */
/* 		    case( WALLGEN2 ):  */
/* 		      SDL_BlitSurface(wall , &src , staticMap , &dest);  break; */
/* 		    case( COLUMN ): */
/* 		      SDL_BlitSurface(column , &src , staticMap , &dest);  break;  */
/* 		    case( NOJOINWALL ):  */
/* 		      SDL_BlitSurface(nojoin , &src , staticMap , &dest);  break;  */
/* 		    default: assert(0); */
/* 		    } */
/* 		} */
/* 	      SDL_BlitSurface(staticMap , NULL , buffer , NULL); */
/* 	    }//end 'mapHasChanged" */

/* 	  if(showBuilders) */
/* 	    { //now we put the Crawlers and Jumpers on the buffer */
/* 	      currentBuilders = pDM -> ShowBuilders( ); */
/* 	      for(i = currentBuilders.begin(); i !=  currentBuilders.end(); ++i) */
/* 		{ */
/* 		  dest.x = scale * ( (*i).ShowX() ); */
/* 		  dest.y = scale * ( (*i).ShowY() ); */
/* 		  switch((*i).ShowType() ) */
/* 		    { */
/* 		    case( CRAWLERUP ):  */
/* 		      SDL_BlitSurface(upact , &src , buffer , &dest);  break; */
/* 		    case( CRAWLERDOWN ):  */
/* 		      SDL_BlitSurface(downact , &src , buffer , &dest);  break; */
/* 		    case( CRAWLERLEFT ):  */
/* 		      SDL_BlitSurface(leftact , &src , buffer , &dest);  break; */
/* 		    case( CRAWLERRIGHT ):  */
/* 		      SDL_BlitSurface(rightact , &src , buffer , &dest);  break; */
/* 		    case( DORMANTCRAWLERUP ):  */
/* 		      SDL_BlitSurface(up , &src , buffer , &dest);  break; */
/* 		    case( DORMANTCRAWLERDOWN ):  */
/* 		      SDL_BlitSurface(down , &src , buffer , &dest);  break; */
/* 		    case( DORMANTCRAWLERLEFT ):  */
/* 		      SDL_BlitSurface(left , &src , buffer , &dest);  break; */
/* 		    case( DORMANTCRAWLERRIGHT ):  */
/* 		      SDL_BlitSurface(right , &src , buffer , &dest);  break; */
/* 		    case( JUMPERUP ):  */
/* 		    case( JUMPERDOWN ):  */
/* 		    case( JUMPERLEFT ):  */
/* 		    case( JUMPERRIGHT ):  */
/* 		      SDL_BlitSurface(jumper , &src , buffer , &dest);  break; */

/* 		    default: assert(0); */
/* 		    } */
/* 		} */
/* 	    } */

/* 	  //now blit the buffer on the screen */
/* 	  SDL_BlitSurface(buffer , NULL , screen , NULL); */
/* 	  SDL_UpdateRect(screen , 0 , 0 , 0 , 0 ); */

/* 	      SDL_Delay(speed); */

/* 	}//end "whileThereAreBuilders" */

/*       SDL_BlitSurface(staticMap , NULL , screen , NULL); //to get rid of the Jumpers in the last iteration */
/*       SDL_UpdateRect(screen , 0 , 0 , 0 , 0 ); */

/*       cout << "All done, close the window to continue\n"; */

/*       while(SDL_WaitEvent(&event) != 0) */
/* 	{ */
/* 	  switch(event.type) */
/* 	    { */
/* 	    case (SDL_QUIT):	       */
/* 	      SDL_FreeSurface(staticMap); */
/* 	      SDL_FreeSurface(buffer); */
/* 	      SDL_FreeSurface(wall); */
/* 	      SDL_FreeSurface(column); */
/* 	      SDL_FreeSurface(nojoin); */
/* 	      SDL_FreeSurface(up); */
/* 	      SDL_FreeSurface(down); */
/* 	      SDL_FreeSurface(left); */
/* 	      SDL_FreeSurface(right); */
/* 	      SDL_FreeSurface(leftact); */
/* 	      SDL_FreeSurface(rightact); */
/* 	      SDL_FreeSurface(upact); */
/* 	      SDL_FreeSurface(downact); */
/* 	      SDL_FreeSurface(jumper); */

/* 	      goto jump; */
/* 	    } */
/* 	} */

/*     jump:        	      SDL_Quit(); */
