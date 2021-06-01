#include <RDGeneral/RDLog.h>
#include <GraphMol/RDKitBase.h> 
#include <GraphMol/Canon.h> 
#include <GraphMol/MonomerInfo.h> 
#include <GraphMol/FileParsers/FileParsers.h>
#include <GraphMol/FileParsers/MolFileStereochem.h>
#include <GraphMol/FileParsers/MolSupplier.h>
#include <GraphMol/FileParsers/MolWriters.h>
#include <GraphMol/FileParsers/ProximityBonds.h>
#include <RDGeneral/FileParseException.h>
#include <RDGeneral/BadFileException.h>
#include <External/INCHI-API/inchi.h>
#include <GraphMol/Depictor/RDDepictor.h>
#include <Geometry/Transform3D.h>
#include <GraphMol/MolTransforms/MolTransforms.h>
#include <GraphMol/Substruct/SubstructMatch.h>
#include <GraphMol/SmilesParse/SmilesWrite.h>


#include <string>
#include <vector>
#include <fstream>   
#include <iomanip>
#include <iostream>
#include <algorithm>

using namespace RDKit;

int main(int argc,char *argv[])
{
  if (argc < 2)
    return 1;
  
  RWMol *mol = MolFileToMol(argv[1],false, false, false);
  std::string inchi;
  std::string aux;
  RDKit::ExtraInchiReturnValues rv;
  try
    {
      inchi = RDKit::MolToInchi(*mol,rv, "/LargeMolecules", true); 
      aux = rv.auxInfoPtr;
    }
  catch(const std::exception &e)
    {
      std::cout << e.what() << std::endl;
    }
  if (!inchi.empty())
    std::cout << inchi << std::endl;
  return 0;
}
