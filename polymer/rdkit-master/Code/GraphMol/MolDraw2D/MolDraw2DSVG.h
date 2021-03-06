//
//  Copyright (C) 2015 Greg Landrum 
//
//   @@ All Rights Reserved @@
//  This file is part of the RDKit.
//  The contents are covered by the terms of the BSD license
//  which is included in the file license.txt, found at the root
//  of the RDKit source tree.
//
// derived from Dave Cosgrove's MolDraw2D
//
// This is a concrete class derived from MolDraw2D that uses RDKit to draw a
// molecule into an SVG file

#ifndef MOLDRAW2DSVG_H
#define MOLDRAW2DSVG_H

#include <iostream>
#include "MolDraw2D.h"

// ****************************************************************************

namespace RDKit {

  class MolDraw2DSVG : public MolDraw2D {

  public :
    MolDraw2DSVG( int width , int height , std::ostream &os ) : 
      MolDraw2D( width , height ) , d_os( os ) { initDrawing(); };

    // set font size in molecule coordinate units. That's probably Angstrom for
    // RDKit. It will turned into drawing units using scale_, which might be
    // changed as a result, to make sure things still appear in the window.
    void setFontSize( float new_size );
    void setColour( const DrawColour &col );

    // not sure if this goes here or if we should do a dtor since initDrawing() is called in the ctor,
    // but we'll start here
    void finishDrawing();

  private :
    std::ostream &d_os;

    void drawLine( const std::pair<float,float> &cds1 ,
                   const std::pair<float,float> &cds2 );
    void drawChar( char c , const std::pair<float,float> &cds );
    void drawString( const std::string &str, const std::pair<float,float> &cds );
    void drawTriangle( const std::pair<float,float> &cds1 ,
                       const std::pair<float,float> &cds2 ,
                       const std::pair<float,float> &cds3 );
    void clearDrawing();

    // using the current scale, work out the size of the label in molecule coordinates
    void getStringSize( const std::string &label , float &label_width ,
                        float &label_height ) const;

    void initDrawing();

  };

}
#endif // MOLDRAW2DSVG_H
