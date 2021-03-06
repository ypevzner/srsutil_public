//
//   @@ All Rights Reserved @@
//  This file is part of the RDKit.
//  The contents are covered by the terms of the BSD license
//  which is included in the file license.txt, found at the root
//  of the RDKit source tree.
//
// Original author: David Cosgrove (AstraZeneca)
//
// This is a concrete class derived from MolDraw2D that uses RDKit to draw a
// molecule into a QPainter.

#ifndef MOLDRAW2DQT_H
#define MOLDRAW2DQT_H

#include "MolDraw2D.h"

class QPainter;
class QString;

// ****************************************************************************

namespace RDKit {

  class MolDraw2DQt : public MolDraw2D {

  public :

    MolDraw2DQt( int width , int height , QPainter &qp );

    // set font size in molecule coordinate units. That's probably Angstrom for
    // RDKit. It will turned into drawing units using scale_, which might be
    // changed as a result, to make sure things still appear in the window.
    void setFontSize( float new_size );
    void setColour( const DrawColour &col );

  private :

    QPainter &qp_;

    void drawLine( const std::pair<float,float> &cds1 ,
                   const std::pair<float,float> &cds2 );
    void drawChar( char c , const std::pair<float,float> &cds );
    void drawTriangle( const std::pair<float,float> &cds1 ,
                       const std::pair<float,float> &cds2 ,
                       const std::pair<float,float> &cds3 );
    void clearDrawing();

    // using the current scale, work out the size of the label in molecule coordinates
    void getStringSize( const std::string &label , float &label_width ,
                        float &label_height ) const;

  };

}
#endif // MOLDRAW2DQT_H
