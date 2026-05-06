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
#include "SDL_Renderer.h"
#include <cassert>
#include <cstdlib>     //for rand()
#include <ctime>       //for time in rand seed
#include <cstdio>      //for file handling
#include <iostream>   //for console traffic

using namespace alifegames;
using namespace std;

//#include<SDL/SDL.h>

//   SDL_Surface* screen;
//   SDL_Surface* buffer;
//   SDL_Rect src , dest;

bool SDL_Renderer::Initialize( DungeonMaker& tDM , int sc)
{	  
  scale = sc;
  if(SDL_Init(SDL_INIT_VIDEO) != 0) 
    {
      printf("Unable to init SDL: %s\n" , SDL_GetError());
      return(false);
    } 
  atexit(SDL_Quit);

  int dX = tDM.GetDimX();
  int dY = tDM.GetDimY();
  screen = SDL_SetVideoMode(dY*scale , dX*scale , 16 , 0);
  if(screen == NULL) 
    { 
      printf("Unable to set video mode: %s\n" , SDL_GetError()); 
      return(false); 
    } 

  //set buffer to the same format as screen 
  //  SDL_PixelFormat* fmt;  //this is now a member of the class
  fmt = screen -> format; 
  buffer = SDL_CreateRGBSurface(SDL_SRCCOLORKEY , dY*scale , dX*scale , 16 , fmt -> Rmask , fmt -> Gmask , fmt -> Bmask ,  fmt -> Amask); 
  if(buffer == NULL) 
    { 
      printf("Unable to allocate video memory: %s\n" , SDL_GetError()); 
      return(false); 
    } 

  time = SDL_GetTicks();

  return(true);
}

Uint32 SDL_Renderer::GetColor( SquareData dat , bool d)
{
  if(d)
    {
      switch(dat)
	{
	case OPEN: return( SDL_MapRGB( fmt , 255 , 255 , 255 ) );
	case CLOSED:  return( SDL_MapRGB( fmt , 0 , 0 , 0 ) );
	  //   OPEN=0 , CLOSED , G_OPEN , G_CLOSED , //GUARANTEED-OPEN AND GUARANTEED-CLOSED
	  //   NJ_OPEN , NJ_CLOSED , NJ_G_OPEN , NJ_G_CLOSED , //NJ = non-join, these cannot be joined br Builders with others of their own kind (
	  //   H_DOOR , V_DOOR ,   //horizontal door, varies over y-axis , vertical door, over x-axis(up and down)
	  //   COLUMN 
     
	case G_OPEN: return( SDL_MapRGB( fmt , 204 , 255 , 204 ) );
	case NJ_OPEN: return( SDL_MapRGB( fmt , 255 , 204 , 204 ) );
	case NJ_G_OPEN: return( SDL_MapRGB( fmt , 255 , 255 , 153 ) );

	case G_CLOSED:  return( SDL_MapRGB( fmt , 102 , 0 , 102 ) );
	case NJ_CLOSED:  return( SDL_MapRGB( fmt , 0 , 102 , 102 ) );
	case NJ_G_CLOSED:  return( SDL_MapRGB( fmt , 0 , 0 , 153 ) );

	case(IR_OPEN):  return( SDL_MapRGB( fmt , 204 , 204 , 204 ) );
	case(IT_OPEN):  return( SDL_MapRGB( fmt , 255 , 204 , 153 ) );
	case(IA_OPEN):  return( SDL_MapRGB( fmt , 255 , 204 , 102 ) );

	case H_DOOR:
	case V_DOOR:  return( SDL_MapRGB( fmt , 153 , 153 , 0 ) );

	case MOB1:   return( SDL_MapRGB( fmt , 204 , 153 , 153 ) );
	case MOB2:   return( SDL_MapRGB( fmt , 255 , 153 , 153 ) );
	case MOB3:   return( SDL_MapRGB( fmt , 255 , 0 , 0 ) );

	case TREAS1:   return( SDL_MapRGB( fmt , 153 , 204 , 153 ) );
	case TREAS2:   return( SDL_MapRGB( fmt , 51 , 204 , 51 ) );
	case TREAS3:   return( SDL_MapRGB( fmt , 0 , 255 , 0 ) );

	case COLUMN: return( SDL_MapRGB( fmt , 0 , 0 , 0 ) );

	default: assert(false);  //new case added?;
	}
    }
  else
    {
      switch(dat)
	{
      	case(IR_OPEN):  return( SDL_MapRGB( fmt , 204 , 204 , 204 ) );
	case(IT_OPEN): 
	case(IA_OPEN):
	case G_OPEN:
	case NJ_OPEN:
	case NJ_G_OPEN:
	case OPEN: return( SDL_MapRGB( fmt , 255 , 255 , 255 ) );

	case COLUMN:
	case G_CLOSED:
	case NJ_CLOSED:
	case NJ_G_CLOSED:
	case CLOSED:  return( SDL_MapRGB( fmt , 0 , 0 , 0 ) );

	case H_DOOR:
	case V_DOOR:  return( SDL_MapRGB( fmt , 153 , 153 , 0 ) );

	case MOB1:   return( SDL_MapRGB( fmt , 204 , 153 , 153 ) );
	case MOB2:   return( SDL_MapRGB( fmt , 255 , 153 , 153 ) );
	case MOB3:   return( SDL_MapRGB( fmt , 255 , 0 , 0 ) );

	case TREAS1:   return( SDL_MapRGB( fmt , 153 , 204 , 153 ) );
	case TREAS2:   return( SDL_MapRGB( fmt , 51 , 204 , 51 ) );
	case TREAS3:   return( SDL_MapRGB( fmt , 0 , 255 , 0 ) );


	default: assert(false);  //new case added?;
	}
    }
}

bool SDL_Renderer::RenderMap( DungeonMaker& tDM , bool d)
{
  SDL_Rect dest;
  dest.w = scale;
  dest.h = scale;
  for(int indX = 0; indX < tDM.GetDimX() ; indX++)
    for(int indY = 0; indY < tDM.GetDimY() ; indY++)
      {
	dest.x = indY * scale; //unfortunately SDL has x running along the horizontal axis, 
	dest.y = indX * scale; //y along the vertical, see our arrangement in Dungeonmaker.h
	if( SDL_FillRect( buffer , &dest , GetColor( tDM.GetMap( indX , indY ) , d ) ) != 0 )
	  return(false);
      }

  SDL_BlitSurface(buffer , NULL , screen , NULL); 
  SDL_UpdateRect(screen , 0 , 0 , 0 , 0 );

  Uint32 elapsedTime = SDL_GetTicks() - time;
  if(300 > elapsedTime)
    SDL_Delay(300 - elapsedTime);
  time = SDL_GetTicks();

  SDL_Event event;
  bool quit = false;
  while(SDL_PollEvent(&event) != 0) 
    { 
      switch(event.type)
	{ 
	case (SDL_QUIT):	       
	  SDL_FreeSurface(buffer); 
	  quit = true;
	} 
    } 
  if(quit)   
    SDL_Quit(); 

  return(true);
}

bool SDL_Renderer::UpdateMap(vector< alifegames::SquareInfo > ChangedSquares , unsigned int tickTime , bool d)
{
  SDL_Rect dest;
  dest.w = scale;
  dest.h = scale;

  for(unsigned int i = 0; i < ChangedSquares.size(); i++)
    {
      dest.x = ChangedSquares[i].yCoord * scale;  //switch coordinates!!!
      dest.y = ChangedSquares[i].xCoord * scale;
      if( SDL_FillRect( buffer , &dest , GetColor( ChangedSquares[i].type , d ) ) != 0 )
	{
	  cout << "SDL_FillRect failed in UpdateMap, bailing out..." << endl;
	  return(false);
	}
    }

  SDL_BlitSurface(buffer , NULL , screen , NULL); 
  SDL_UpdateRect(screen , 0 , 0 , 0 , 0 );

  Uint32 elapsedTime = SDL_GetTicks() - time;
  if(tickTime > elapsedTime)
    SDL_Delay(tickTime - elapsedTime);
  time = SDL_GetTicks();

  SDL_Event event;
  while(SDL_PollEvent(&event) != 0) 
    { 
      switch(event.type)
	{ 
	case (SDL_QUIT):	       
	  SDL_FreeSurface(buffer); 
	  return(false);
	} 
    } 

  return(true);
}

bool SDL_Renderer::ShowMovie(vector< vector< alifegames::SquareInfo > > movie , int dX , int dY , unsigned int tickTime , SquareData bg , bool d)
{
  SDL_Rect dest;
  dest.w = scale;
  dest.h = scale;
  for(int indX = 0; indX < dX ; indX++)
    for(int indY = 0; indY < dY ; indY++)
      {
	dest.x = indY * scale; //unfortunately SDL has x running along the horizontal axis, 
	dest.y = indX * scale; //y along the vertical, see our arrangement in Dungeonmaker.h
	if( SDL_FillRect( buffer , &dest , GetColor( bg  , d) ) != 0 )
	  return(false);
      }

  SDL_BlitSurface(buffer , NULL , screen , NULL); 
  SDL_UpdateRect(screen , 0 , 0 , 0 , 0 );

  for(unsigned int i = 0; i < movie.size(); i++)
    {
      if(!UpdateMap(movie[i] , tickTime , d) )
	return(false);
    }
  return(true);
}



bool SDL_Renderer::HangInThere()
{
  SDL_Event event;
  while(SDL_WaitEvent(&event) != 0) 
    { 
      switch(event.type)
	{ 
	case (SDL_QUIT):	       
	  SDL_FreeSurface(buffer); 
	  return(false);
	} 
    } 
  return(true);  //should never get here 
}

