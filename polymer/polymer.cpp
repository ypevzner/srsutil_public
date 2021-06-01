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
#include <GraphMol/QueryAtom.h>

#include "pugixml.hpp"

#include <string>
#include <vector>
#include <fstream>   
#include <iomanip>
#include <iostream>
#include <algorithm>

#include <tclap/CmdLine.h>


using namespace RDKit;

enum EFragmentType
  {
    eDisconnected = 0,
    eEndpoint,
    eSRU,
    eConnection
  };
  

bool process_mol(const std::string &fname, const std::string &xml, const std::string &out, bool print_connection, const std::string &output_sru,
		 bool keep_wildcard, bool split_fragments, bool canonicalize, bool branched);

bool process_sdf( const std::string &fname, const std::string &ofile, bool print_connection, const std::string &output_sru,
		  bool keep_wildcard, bool split_fragments, bool canonicalize, bool branched);

bool ProcessMolFragment(RWMol *&mol, bool print_connection, int num_atoms, const std::string &output_sru, bool keep_wildcard, bool canonicalize, bool branched);

void FindBondSGroups(ROMol *mol, std::set<int> &sss_set);

bool BelongsToMultipleSgroups(ROMol *mol, std::set<int> &connections_set, const std::vector<std::pair<unsigned int, unsigned int> > &connections);

void GetConnectingAtomsAndBonds(ROMol *mol,
				std::vector<unsigned int> &bonds,
				std::vector<unsigned int> &atoms,
				int sss
				);

void MarkConnectingAtoms(ROMol *mol, const Bond *bond1, const Bond *bond2, int a1, int a2, std::vector<int> &sgroups_bond1, std::vector<int> &sgroups_bond2);

RWMol *GetSRU(RWMol *mol,
	      unsigned int &aid1, unsigned int &aid2,
	      int &fragment,
	      std::vector<boost::shared_ptr<ROMol> > &frags,
	      const Bond *bond1,
	      const Bond *bond2
	      );

void FindBreakableBonds(ROMol *mol1, unsigned int aid1, unsigned int aid2, std::vector<std::pair<unsigned int, unsigned int> > &connections);

std::string FindMinimumInChI( RWMol *mol1, unsigned int start, unsigned int finish,
			      const std::vector<std::pair<unsigned int, unsigned int> > &connections,
			      unsigned int &min_a1,  unsigned int &min_a2,
			      unsigned int &min_senior,  unsigned int &min_junior);

std::string GetMinimumInChI( RWMol *mol1, unsigned int start, unsigned int finish);

void GetLeftRightCenterFragments(boost::shared_ptr<ROMol> sru, ROMol *old_mol, unsigned int min_a1,  unsigned int min_a2, 
				 RWMol *&mol1, RWMol *&mol_left, RWMol *&mol_right, double &d1_length, double &d2_length);

void ClearAtomSgroup(ROMol *mol, int sss);

void AddStartAndFinishFragments(std::vector<boost::shared_ptr<ROMol> > &frags, RWMol *&mol_left, RWMol *&mol_right, bool keep_wildcard);

void ConnectFragment(RWMol *mol, const std::string &connected_start, const std::string &start, const std::string &new_end, const std::string &new_connected_start,
		     int sss, std::vector<int> &sgroups_bond);

void GetConnectionAtoms(ROMol *mol1, int &a1, int &a2, int &a3, int &a4);

void CreateBond(RWMol *mol1, int a1, int a2, int sss, const std::vector<int> &sgroups_bond);


void MoveBrackets(RWMol *mol1, std::vector<RDGeom::Point3D>& coords, bool first_pass);


void ClearAtomProp(ROMol *mol, const std::string &prop);

RDGeom::Point3D GetAtomPosition(ROMol *mol, const std::string &prop);

void parse_xml(std::istream& stream);

bool RemoveSRU(RWMol *mol, ROMol *sru, const std::string &prop, std::vector<int> &sgroups_bond);
bool RemovePartialSRU(RWMol *mol_left, RWMol *mol_right, RWMol *sru, std::vector<int> &sgroups_bond1, std::vector<int> &sgroups_bond2);

void RotateMolToBrackets(RWMol *mol, int sss);

void OutputSRU(SDWriter *writer, RWMol *mol, const std::string &min_inchi, std::set<std::string> &seen_inchi,
	       const std::map<std::string, std::set<std::string> > &inchi_to_label);

void SplitFragments(RWMol *orig_mol, std::vector<boost::shared_ptr<ROMol> > &separate_fragments,
		    bool is_chiral_flag, int chirality_flag, bool keep_wildcard, int polymer_label,
		    const std::vector<boost::shared_ptr<ROMol> > &salts);

void SaveBrackets(ROMol *mol, std::vector<std::vector<double> >  &sgroups);

void RestoreBrackets(ROMol *mol, const std::vector<std::vector<double> >  &sgroups, const std::set<int> &sss_set);

void FindAtomSGroups(ROMol *mol, std::set<int> &sss_set);

void ClearSgroupsFromSplitFormerSRUs(boost::shared_ptr<ROMol> m, EFragmentType fragment_type);

void ReconnectGraftSrus(std::vector<boost::shared_ptr<ROMol> > &separate_fragments, std::vector<EFragmentType> &fragment_types, bool keep_wildcard);

void MarkOldAtomIds(RWMol *mol);

void ClearOldAtomIds(RWMol *mol);

void RemoveAllSGroups(ROMol* m);

std::vector<std::string> GetConnectingAtomsSRU(ROMol *m, std::map<int,int> &canonical, std::vector<int> &aids);
std::string GetConnectingAtoms(ROMol *m, std::map<int,int> &canonical, std::vector<int> &aids);

void KeepSGroup(ROMol *mol, const std::set<int> &sss_set);

std::vector<boost::shared_ptr<ROMol> > AddStarAtoms(std::vector<boost::shared_ptr<ROMol> > &separate_fragments, bool keep_wildcard);

int main(int argc,char *argv[])
{
  //RDLog::InitLogs();
  std::string input,output,xml;
  bool is_sdf = false;
  bool print_connection = false;
  std::string output_sru;
  bool keep_wildcard = false;
  bool split_fragments = true;
  bool canonicalize = true;
  bool branched = false;
  try
    {
      TCLAP::CmdLine cmd("Polymer canonicalization tool", ' ', "1.0");
      TCLAP::UnlabeledValueArg<std::string>  input_arg( "input.mol", "Input MOL file", true, "", "filename"  );
      cmd.add( input_arg );
      TCLAP::UnlabeledValueArg<std::string>  output_arg( "output.mol", "Output MOL file", true, "", "filename"  );
      cmd.add( output_arg );
      TCLAP::SwitchArg sdf_arg("","sdf","Input and output are SD files"); 
      cmd.add( sdf_arg );
      TCLAP::ValueArg<std::string> xml_arg("","xml","External XML file name",false,"","filename");
      cmd.add( xml_arg );
      TCLAP::SwitchArg connection_arg("","connection","Print out connection atom numbers"); 
      cmd.add( connection_arg );
      TCLAP::ValueArg<std::string> sru_arg("","sru","Output found SRUs to file name",false,"","filename");
      cmd.add( sru_arg );
      TCLAP::SwitchArg keep_wildcard_arg("","keep-wildcards","Keep wildcard atoms unmodified"); 
      cmd.add( keep_wildcard_arg );
      TCLAP::SwitchArg do_not_split_arg("","do-not-split","Do not split fragments"); 
      cmd.add( do_not_split_arg );
      TCLAP::SwitchArg do_not_canonicalize_arg("","do-not-canonicalize","Do not canonicalize molecules"); 
      cmd.add( do_not_canonicalize_arg );
      TCLAP::SwitchArg branched_arg("","branched","Process branched polymers"); 
      cmd.add( branched_arg );

      cmd.parse( argc, argv );
      input = input_arg.getValue();
      output = output_arg.getValue();
      is_sdf = sdf_arg.getValue();
      xml = xml_arg.getValue();
      print_connection = connection_arg.getValue();
      output_sru = sru_arg.getValue();
      keep_wildcard = keep_wildcard_arg.getValue();
      split_fragments = !do_not_split_arg.getValue();
      canonicalize = !do_not_canonicalize_arg.getValue();
      branched = branched_arg.getValue();
    }  catch (TCLAP::ArgException &e)  // catch any exceptions
    { std::cerr << "error: " << e.error() << " for arg " << e.argId() << std::endl; exit(1);}
  
  boost::logging::disable_logs("rdApp.*");
  bool err = false;
  if (is_sdf)
    {
      err = process_sdf(input, output, print_connection, output_sru, keep_wildcard, split_fragments, canonicalize, branched);
    }
  else
    {
      err = process_mol(input, xml, output, print_connection, output_sru, keep_wildcard, split_fragments, canonicalize, branched);
    }

  if (err)
    return 1;
  return 0;
}

void FindBondSGroups(ROMol *mol, std::set<int> &sss_set)
{
  for(ROMol::BondIterator bondIt=mol->beginBonds(); bondIt!=mol->endBonds();++bondIt)
    {
      std::vector<int> sgroups;
      if ((*bondIt)->getPropIfPresent("_SGroup", sgroups))
	{
	  if (!sgroups.empty()) 
	    {
	      sss_set.insert(sgroups.begin(), sgroups.end());
	    }
	}
    }
}

bool BelongsToMultipleSgroups(ROMol *mol, std::set<int> &connections_set, const std::vector<std::pair<unsigned int, unsigned int> > &connections)
{
  bool err(false);
  unsigned int aid1, aid2;
  for (size_t i = 0; i < connections.size(); i++)
    {
      unsigned int a1 = connections[i].first;
      unsigned int a2 = connections[i].second;
      if (mol->getAtomWithIdx(a1)->hasProp("_old_aid")) 
	{
	  mol->getAtomWithIdx(a1)->getProp("_old_aid", aid1);
	  if (connections_set.find(aid1) != connections_set.end())
	    {
	      std::cerr << "Atom " << aid1 + 1 << " belongs to multiple SRUs" << std::endl; 
	      err = true;
	    }
	}
      if (mol->getAtomWithIdx(a2)->hasProp("_old_aid"))
	{
	  mol->getAtomWithIdx(a2)->getProp("_old_aid", aid2); 
	  if (connections_set.find(aid2) != connections_set.end())
	    {
	      std::cerr << "Atom " << aid2 + 1 << " belongs to multiple SRUs" << std::endl; 
	      err = true;
	    }
	}      
    }
  for (size_t i = 0; i < connections.size(); i++)
    {
      unsigned int a1 = connections[i].first;
      unsigned int a2 = connections[i].second;
      if (mol->getAtomWithIdx(a1)->hasProp("_old_aid")) 
	{
	  mol->getAtomWithIdx(a1)->getProp("_old_aid", aid1);
	  connections_set.insert(aid1);
	}
      if (mol->getAtomWithIdx(a2)->hasProp("_old_aid"))
	{
	  mol->getAtomWithIdx(a2)->getProp("_old_aid", aid2);
	    connections_set.insert(aid2);
	}
    }
  return err;
}

void PrintSGroups(ROMol *mol)
{
  for(ROMol::BondIterator bondIt=mol->beginBonds(); bondIt!=mol->endBonds();++bondIt)
    {
      std::vector<int> sgroups;
      if ((*bondIt)->getPropIfPresent("_SGroup", sgroups))
	{
	  for (size_t i = 0; i < sgroups.size(); i++)
	    {
	      std::cerr << sgroups[i] << " ";
	    }
	  std::cerr<<std::endl;
	}
    }
}


bool TestIfSgroupPresent(const std::vector<int> sgroups, int sss)
{
  bool res = false;
  for (size_t i = 0; i < sgroups.size(); i++)
    if (sgroups[i] == sss)
      {
	res = true;
	break;
      }
  return res;
}

void GetConnectingAtomsAndBonds(ROMol *mol,
				std::vector<unsigned int> &bonds,
				std::vector<unsigned int> &atoms,
				int sss
				) 
{
  std::vector<int> sgroups;
  for(ROMol::BondIterator bondIt=mol->beginBonds(); bondIt!=mol->endBonds();++bondIt)
      {
	unsigned int aid1 = (*bondIt)->getBeginAtomIdx();
	unsigned int aid2 = (*bondIt)->getEndAtomIdx();
	const Atom *atom1 = mol->getAtomWithIdx (aid1);
	const Atom *atom2 = mol->getAtomWithIdx (aid2);
	bool sss1 = false;
	sgroups.clear();
	if (atom1->getPropIfPresent("_SGroup", sgroups))
	  {
	    sss1 = TestIfSgroupPresent(sgroups, sss);
	  }
	bool sss2 = false;
	sgroups.clear();
	if (atom2->getPropIfPresent("_SGroup", sgroups))
	  {
	    sss2 = TestIfSgroupPresent(sgroups, sss);
	  }
	if (!sss1 && sss2)
	  {
	    bonds.push_back((*bondIt)->getIdx());
	    atoms.push_back(aid2);
	  }  
	if (sss1 && !sss2)
	  {
	    bonds.push_back((*bondIt)->getIdx());
	    atoms.push_back(aid1);
	  }  
      }
}

void MarkConnectingAtoms(ROMol *mol, const Bond *bond1, const Bond *bond2, int a1, int a2, std::vector<int> &sgroups_bond1, std::vector<int> &sgroups_bond2 )
{
  RDKit::Conformer &conf =mol->getConformer();
  std::vector<RDGeom::Point3D> &coords = conf.getPositions();
  RDGeom::Point3D p1 = coords[a1];
  RDGeom::Point3D p2 = coords[a2];
  if (p2[0] < p1[0])
    std::swap(a1,a2);
  bool swap_bond_sgroups = false;
  bond1->getPropIfPresent("_SGroup", sgroups_bond1);
  bond2->getPropIfPresent("_SGroup", sgroups_bond2);

  mol->getAtomWithIdx(a1)->setProp("_StartAtom",true);
  mol->getAtomWithIdx(a2)->setProp("_EndAtom",true);

  if (bond1->getBeginAtomIdx() ==  a1 ||  bond1->getEndAtomIdx() == a1)
    {
      mol->getAtomWithIdx(bond1->getOtherAtomIdx(a1))->setProp("_ConnectedStartAtom",true);
    }
  if (bond2->getBeginAtomIdx() ==  a1 ||  bond2->getEndAtomIdx() == a1)
    {
      mol->getAtomWithIdx(bond2->getOtherAtomIdx(a1))->setProp("_ConnectedStartAtom",true);
      swap_bond_sgroups = true;
    }
  if (bond1->getBeginAtomIdx() ==  a2 ||  bond1->getEndAtomIdx() == a2)
    {
      mol->getAtomWithIdx(bond1->getOtherAtomIdx(a2))->setProp("_ConnectedEndAtom",true);
      swap_bond_sgroups = true;
    }
  if (bond2->getBeginAtomIdx() ==  a2 ||  bond2->getEndAtomIdx() == a2)
    {
      mol->getAtomWithIdx(bond2->getOtherAtomIdx(a2))->setProp("_ConnectedEndAtom",true);
    }
  if (swap_bond_sgroups)
    {
      std::swap(sgroups_bond1, sgroups_bond2);
      std::swap(bond1, bond2);
    }
}

void MarkOldAtomIds(RWMol *mol)
{
  for(ROMol::AtomIterator atomIt=mol->beginAtoms(); atomIt!=mol->endAtoms();++atomIt)
    {
      (*atomIt)->setProp("_old_aid", (*atomIt)->getIdx());
    }
}

void ClearOldAtomIds(RWMol *mol)
{
  for(ROMol::AtomIterator atomIt=mol->beginAtoms(); atomIt!=mol->endAtoms();++atomIt)
    {
      if ((*atomIt)->hasProp("_old_aid"))
	(*atomIt)->clearProp("_old_aid");
    }
}

RWMol *GetSRU(RWMol *mol,
	      unsigned int &aid1, unsigned int &aid2,
	      int &fragment,
	      std::vector<boost::shared_ptr<ROMol> > &frags,
	      const Bond *bond1,
	      const Bond *bond2
	      )
{
  mol->removeBond(bond1->getBeginAtomIdx(), bond1->getEndAtomIdx());
  mol->removeBond(bond2->getBeginAtomIdx(), bond2->getEndAtomIdx());
  frags = MolOps::getMolFrags(*mol,false,NULL);
  for (unsigned int i=0; i<frags.size(); i++)
    {
      for(ROMol::AtomIterator atomIt=frags[i]->beginAtoms(); atomIt!=frags[i]->endAtoms();++atomIt)
	{
	  if ( (*atomIt)->hasProp("_StartAtom"))
	    {
	      fragment = i;
	      aid1 = (*atomIt)->getIdx();
	    }
	  if ( (*atomIt)->hasProp("_EndAtom"))
	    {
	      aid2 = (*atomIt)->getIdx();
	    }
	}
    }
  if (fragment >= 0)
    return new RWMol(*(frags[fragment]));

  return NULL;
}

void FindBreakableBonds(ROMol *mol1, unsigned int aid1, unsigned int aid2, std::vector<std::pair<unsigned int, unsigned int> > &connections)
{
  MolOps::findSSSR(*mol1);
  RingInfo *ringInfo = mol1->getRingInfo();
  std::list< int > path = RDKit::MolOps::getShortestPath (*mol1, aid1, aid2);
  std::list<int>::iterator i = path.begin();
  unsigned int prev = *i;
  ++i;
  for ( ; i != path.end(); ++i)
    {
      Bond *bond = mol1->getBondBetweenAtoms(prev, *i);
      if (bond && ringInfo->numBondRings(bond->getIdx()) == 0 && bond->getBondType() == Bond::SINGLE)
	{
	  connections.push_back(std::make_pair(prev,*i));
	}
      prev = *i;
    }
}

std::vector<std::string> split(const std::string &s, char delim) 
{
  std::vector<std::string> elems;
  std::stringstream ss(s);
  std::string item;
  while (getline(ss, item, delim)) 
    {
      elems.push_back(item);
    }
  return elems;
}

// based on https://static-content.springer.com/esm/art%3A10.1186%2F1758-2946-4-22/MediaObjects/13321_2012_342_MOESM1_ESM.py
// from https://jcheminf.springeropen.com/articles/10.1186/1758-2946-4-22#MOESM1

std::map<int,int> input_to_canonical(std::string inchi,  std::string aux)
{
  std::map<int,int> canonical;
  size_t recon_start = inchi.find("/r");
  std::vector<std::string> split_inchi;
  if (recon_start == std::string::npos)
    split_inchi = split(inchi, '/');
  else
    {
      split_inchi.push_back("");
      std::vector<std::string> tmp = split(inchi.substr(recon_start), '/');
      for (size_t i = 0; i < tmp.size(); i++)
	split_inchi.push_back(tmp[i]);
      size_t recon_start_b = aux.find("/R:");
      if (recon_start_b == std::string::npos)
	return canonical;
      aux = aux.substr(recon_start_b+1);
    }

  std::vector<std::string>  split_aux = split(aux, '/');
  if (split_aux[2][0] != 'N')
    return canonical;

  std::vector<int> canlabels;
  {
    std::vector<std::string> x = split(split_aux[2].substr(2), ';');
    for (size_t i = 0; i < x.size(); i++)
      {
	std::vector<std::string> tmp = split(x[i], ',');
	for (size_t j = 0; j < tmp.size(); j++)
	  canlabels.push_back(atoi(tmp[j].c_str()));
      }
  }

  size_t fixedH_start = aux.find("/F");
  if (fixedH_start != std::string::npos)
    {
      std::vector<std::string> tmp = split(aux.substr(fixedH_start + 3), '/');
      std::string broken = tmp[0];
      std::vector<std::string>  mols = split(broken,';');
      std::vector<int>  new_canlabels;
      size_t tot = 0;
      for (size_t i = 0; i < mols.size(); i++)
	{
	  std::string mol = mols[i];
	  if (mol == "m")
	    mol = "1m";
	  if (!mol.empty() && *mol.rbegin() == 'm')
	    {
	      int mult = atoi(mol.substr(0, mol.size() - 1).c_str());
	      for (size_t j = tot; j < tot + mult; j++)
		new_canlabels.push_back(canlabels[j]);
	      tot += mult;
	    }
	  else
	    {
	      std::vector<std::string> tmp2 = split(mol, ',');
	      for (size_t j = 0; j < tmp2.size(); j++)
		new_canlabels.push_back(atoi(tmp2[j].c_str()));
	      tot++;
	    }
	}
      canlabels.clear();
      canlabels = new_canlabels;
    }

  for (size_t i = 0; i < canlabels.size(); i++)
    canonical[canlabels[i] - 1] = i;
  return canonical;
}

bool IsInHeteroRing(RWMol *mol1, unsigned int a, const std::vector< std::vector<int> > &rings)
{
  bool hetero_ring = false;
  for (size_t i = 0; i < rings.size(); i++)
    {
      bool found = false;
      for (size_t j = 0; j < rings[i].size(); j++)
	if (rings[i][j] == a)
	  {
	    found = true;
	    break;
	  }
      if (found)
	{
	   for (size_t j = 0; j < rings[i].size(); j++)
	     if (mol1->getAtomWithIdx(rings[i][j])->getAtomicNum() != 1 && mol1->getAtomWithIdx(rings[i][j])->getAtomicNum() != 6)
	       {
		 hetero_ring = true;
		 break;
	       }
	}
      if (hetero_ring)
	break;
    }
  return hetero_ring;
}

size_t LargestRing(unsigned int a, const std::vector< std::vector<int> > &rings, bool hetero, RWMol *mol1)
{
  size_t r = 0;
  for (size_t i = 0; i < rings.size(); i++)
    {
      bool found = false;
      bool is_hetero = false;
      for (size_t j = 0; j < rings[i].size(); j++)
	{
	  if (rings[i][j] == a)
	    {
	      found = true;
	    }
	  if (mol1->getAtomWithIdx(rings[i][j])->getAtomicNum() != 1 && mol1->getAtomWithIdx(rings[i][j])->getAtomicNum() != 6)
	    {
	      is_hetero = true;
	    }
	}
      if ((hetero && !is_hetero) || (!hetero && is_hetero))
	continue;
      if (found && rings[i].size() > r)
	{
	  r = rings[i].size();
	}
    }
  return r;
}


int HeteroInRingRank(int n)
{
  /*
    Max rank for in-ring atom is 216 which is achieved for N (element number 7 in Periodic system & erank_rule2[] ),
    then goes O with rank 215 (element number 8), and so on... lowest rank is 1 for H .

    This follows to IUPAC rule 2 [Pure Appl. Chem., Vol. 74, No. 10, 2002, p. 1926] which states:
    a. a ring or ring system containing nitrogen;
    b. a ring or ring system containing the heteroatom occurring earliest in the order given in Rule 4;
        ( which is     O > S > Se > Te > N > P > As > Sb > Bi > Si > Ge > Sn > Pb > B > Hg )
    ...

*/
static const  int erank_rule2[] = { 0,1,198,197,196,202,2,216,215,191,190,189,188,187,206,210,214,183,182,181,180,179,178,177,176,
				    175,174,173,172,171,170,169,205,209,213,165,164,163,162,161,160,159,158,157,156,155,154,153,152,
				    151,204,208,212,147,146,145,144,143,142,141,140,139,138,137,136,135,134,133,132,131,130,129,128,
				    127,126,125,124,123,122,121,201,119,203,207,116,115,114,113,112,111,110,109,108,107,106,105,104,
				    103,102,101,100,99,98,97,96,95,94,93,92,91,90,89,88,87,86,85,84,83,82,81};
 if ( n < sizeof(erank_rule2)/sizeof(erank_rule2[0]))
   return erank_rule2[n];
 return 0;
}

int HeteroNotInRingRank(int n)
{
/*
    Max rank for chain atom is 215 which is achieved for O (element number 8 in Periodic system & erank_rule4[] ),
    then goes N with rank 212 (element number 8), and so on... lowest rank is 1 for H .
    
    This follows to IUPAC rule 4 [Pure Appl. Chem., Vol. 74, No. 10, 2002, p. 1927] which states:
    O > S > Se > Te > N > P > As > Sb > Bi > Si > Ge > Sn > Pb > B > Hg
    Note: Other heteroatoms may be placed within this order as indicated by their positions in the
    periodic table [5].
*/
  static const int erank_rule4[] = { 0,1,198,197,196,202,2,211,215,191,190,189,188,187,206,210,214,183,182,181,180,179,178,177,176,
				     175,174,173,172,171,170,169,205,209,213,165,164,163,162,161,160,159,158,157,156,155,154,153,152,
				     151,204,208,212,147,146,145,144,143,142,141,140,139,138,137,136,135,134,133,132,131,130,129,128,
				     127,126,125,124,123,122,121,201,119,203,207,116,115,114,113,112,111,110,109,108,107,106,105,104,
				     103,102,101,100,99,98,97,96,95,94,93,92,91,90,89,88,87,86,85,84,83,82,81};
  if ( n < sizeof(erank_rule4)/sizeof(erank_rule4[0]))
    return erank_rule4[n];
  return 0;  
}

int SeniorInRing(unsigned int a, const std::vector< std::vector<int> > &rings, RWMol *mol1)
{
  int r = 0;
  for (size_t i = 0; i < rings.size(); i++)
    {
      bool found = false;
      for (size_t j = 0; j < rings[i].size(); j++)
	if (rings[i][j] == a)
	  {
	    found = true;
	    break;
	  }
      if (found)
	{
	  int senior = 0;
	  for (size_t j = 0; j < rings[i].size(); j++)
	    {
	      if (mol1->getAtomWithIdx(rings[i][j])->getAtomicNum() == 6)
		continue;
	      int rank = HeteroInRingRank(mol1->getAtomWithIdx(rings[i][j])->getAtomicNum());
	      if (rank > senior)
		senior = rank;
	    }
	  if (senior > r)
	    r = senior;
	}
    }
  return r;
}

bool HigherRank(RWMol *mol1, unsigned int a1, unsigned int a2)
{

  RingInfo *ringInfo = mol1->getRingInfo();
  const std::vector< std::vector<int> > rings = ringInfo->atomRings();

  bool is_a1_in_hetero_ring = IsInHeteroRing(mol1, a1, rings);
  bool is_a2_in_hetero_ring = IsInHeteroRing(mol1, a2, rings);
  
  bool is_a1_hetero_not_in_ring = mol1->getAtomWithIdx(a1)->getAtomicNum() != 1 && mol1->getAtomWithIdx(a1)->getAtomicNum() != 6 && ringInfo->numAtomRings(a1) == 0;
  bool is_a2_hetero_not_in_ring = mol1->getAtomWithIdx(a2)->getAtomicNum() != 1 && mol1->getAtomWithIdx(a2)->getAtomicNum() != 6 && ringInfo->numAtomRings(a2) == 0;
  
  bool is_a1_carbon_in_ring = mol1->getAtomWithIdx(a1)->getAtomicNum() == 6 && ringInfo->numAtomRings(a1) != 0;
  bool is_a2_carbon_in_ring = mol1->getAtomWithIdx(a2)->getAtomicNum() == 6 && ringInfo->numAtomRings(a2) != 0;

  if (is_a1_in_hetero_ring && !is_a2_in_hetero_ring)
    return true;
  if (!is_a1_in_hetero_ring && is_a2_in_hetero_ring)
    return false;
  if (is_a1_in_hetero_ring && is_a2_in_hetero_ring)
    {
      int r1 = SeniorInRing(a1, rings, mol1);
      int r2 = SeniorInRing(a2, rings, mol1);
      if (r1 == r2)
	return LargestRing(a1, rings, true, mol1) > LargestRing(a2, rings, true, mol1);
      return r1 > r2;
    }
  if (is_a1_hetero_not_in_ring && !is_a2_hetero_not_in_ring)
    return true;
  if (!is_a1_hetero_not_in_ring && is_a2_hetero_not_in_ring)
    return false;
  if (is_a1_hetero_not_in_ring && is_a2_hetero_not_in_ring)
    {
      int r1 = HeteroNotInRingRank(mol1->getAtomWithIdx(a1)->getAtomicNum());
      int r2 = HeteroNotInRingRank(mol1->getAtomWithIdx(a2)->getAtomicNum());
      return r1 > r2;
    }
  if (is_a1_carbon_in_ring && !is_a2_carbon_in_ring)
    return true;
  if (!is_a1_carbon_in_ring && is_a2_carbon_in_ring)
    return false;
  if (is_a1_carbon_in_ring && is_a2_carbon_in_ring)
    {
      int r1 = LargestRing(a1, rings, false, mol1);
      int r2 = LargestRing(a2, rings, false, mol1);
      return r1 > r2;
    }
  return false;
}

void GetSeniorJuniorAtoms(RWMol *mol1, unsigned int a1, unsigned int a2, unsigned int &senior, unsigned int &junior, std::map<int,int> &canonical)
{
  senior = a1;
  junior = a2;
  if (HigherRank(mol1, a1, a2))
    return;
  if (HigherRank(mol1, a2, a1))
    {
      std::swap(senior, junior);
      return;
    }
  if (canonical[a1] > canonical[a2])
    return;
  std::swap(senior, junior);
}

bool CompareAtomPairs(RWMol *mol1, unsigned int senior, unsigned int junior, unsigned int min_senior, unsigned int min_junior, std::map<int,int> &canonical)
{
  if (HigherRank(mol1, senior, min_senior))
    return true;
  if (HigherRank(mol1, min_senior, senior))
    return false;
  if (HigherRank(mol1, junior, min_junior))
    return false;
  if (HigherRank(mol1, min_junior, junior))
    return true;
  if (canonical[junior] < canonical[min_junior]) 
    return true;
  return false;
}

std::string FindMinimumInChI( RWMol *mol1, unsigned int start, unsigned int finish,
		       const std::vector<std::pair<unsigned int, unsigned int> > &connections,
			      unsigned int &min_a1,  unsigned int &min_a2,
			      unsigned int &min_senior, unsigned int &min_junior)
{
  std::string inchi;
  std::string aux;
  RDKit::ExtraInchiReturnValues rv;
  try 
  {
      inchi = MolTextToInchi(*mol1, rv, "/NPZz"); 
      aux = rv.auxInfoPtr;
   } catch(...) {}
  if (inchi.empty())
    {
      std::cerr << "Molecule is not InChI-able" << std::endl;
      return inchi;
    }
  std::map<int,int> canonical = input_to_canonical(inchi,  aux);
  min_a1 = start;
  min_a2 = finish;
  
  MolOps::findSSSR(*mol1);
  GetSeniorJuniorAtoms(mol1, min_a1, min_a2, min_senior, min_junior, canonical);
  for (size_t i = 0; i < connections.size(); i++)
    {
      unsigned int a1 = connections[i].first;
      unsigned int a2 = connections[i].second;
      unsigned int senior, junior;
      GetSeniorJuniorAtoms(mol1, a1, a2, senior, junior, canonical);      

      if (CompareAtomPairs(mol1, senior, junior, min_senior, min_junior, canonical))
	{
	  min_a1 = a1;
	  min_a2 = a2;
	  min_senior = senior;
	  min_junior = junior;
	}
    }

  if (min_a1 != start && min_a2 != finish)
    {
      mol1->removeBond(min_a1, min_a2);
      mol1->addBond(start,finish,Bond::SINGLE);
    }

  std::string min_inchi;
  try
    {
      min_inchi = MolTextToInchi(*mol1, rv, "/NPZz"); 
    } catch(...) {}
  
  if (min_inchi.empty())
    {
      std::cerr << "Modification is not InChI-able" << std::endl;
    }
  
  return min_inchi;
}

std::string GetMinimumInChI( RWMol *mol1, unsigned int start, unsigned int finish)
{
  std::string min_inchi;  
  RDKit::ExtraInchiReturnValues rv;
  
  try
    {
      min_inchi = MolTextToInchi(*mol1, rv, "/NPZz");
    } catch(...) {}
  return min_inchi;
}

void GetLeftRightCenterFragments(boost::shared_ptr<ROMol> sru, ROMol *old_mol, unsigned int min_a1,  unsigned int min_a2, 				
				 RWMol *&mol1, RWMol *&mol_left, RWMol *&mol_right, double &d1_length, double &d2_length)
{
  mol1 = new RWMol(*sru); 
  if (!sru->getBondBetweenAtoms(min_a1, min_a2))
    {
      mol1->getAtomWithIdx(min_a1)->setProp("_NewStartAtom",true);
      mol1->getAtomWithIdx(min_a2)->setProp("_NewEndAtom",true);
      return;
    }
  mol1->getAtomWithIdx(min_a2)->setProp("_NewStartAtom",true);
  mol1->getAtomWithIdx(min_a1)->setProp("_NewEndAtom",true);
  mol1->removeBond(min_a1, min_a2); 
  std::vector<boost::shared_ptr<ROMol> > new_frags = MolOps::getMolFrags(*mol1,false,NULL);
  for (unsigned int i=0; i<new_frags.size(); i++)
    {
      for(ROMol::AtomIterator atomIt=new_frags[i]->beginAtoms(); atomIt!=new_frags[i]->endAtoms();++atomIt)
	{
	  if ( (*atomIt)->hasProp("_StartAtom"))
	    {
	      mol_left = new RWMol(*new_frags[i]);
		    }
	  if ( (*atomIt)->hasProp("_EndAtom"))
	    {
	      mol_right = new RWMol(*new_frags[i]);
	    }
	}
    }

  delete mol1;
  mol1 = new RWMol(*mol_left);
  RDGeom::Point3D v_old_start = GetAtomPosition(mol_left, "_StartAtom");
  RDGeom::Point3D v_old_connected_start = GetAtomPosition(old_mol, "_ConnectedStartAtom");
  RDGeom::Point3D v_old_end = GetAtomPosition(mol_right, "_EndAtom");
  RDGeom::Point3D v_old_connected_end = GetAtomPosition(old_mol, "_ConnectedEndAtom");
  RDGeom::Point3D d1 = v_old_start - v_old_connected_start;
  d1_length = d1.length();
  RDGeom::Point3D d2 = v_old_end - v_old_connected_end;
  d2_length = d2.length();

  RDGeom::Transform3D t_frag;
  t_frag.SetTranslation(v_old_connected_end - v_old_start);
  MolTransforms::transformMolsAtoms(mol1,t_frag); 

  mol1->insertMol(*mol_right);
  unsigned int aid1, aid2;
  for(ROMol::AtomIterator atomIt=mol1->beginAtoms(); atomIt!=mol1->endAtoms();++atomIt)
	{
	  if ( (*atomIt)->hasProp("_StartAtom"))
	    {
	      aid1 = (*atomIt)->getIdx();
	    }
	  if ( (*atomIt)->hasProp("_EndAtom"))
	    {
	      aid2 = (*atomIt)->getIdx();
	    }
	}
  mol1->addBond(aid1,aid2,Bond::SINGLE);
}

void ClearAtomSgroup(ROMol *mol, int sss) 
{
  if (!mol)
    return;
  for(ROMol::AtomIterator atomIt=mol->beginAtoms(); atomIt!=mol->endAtoms();++atomIt)
    {
      std::vector<int> sgroups;
      if ((*atomIt)->getPropIfPresent("_SGroup", sgroups))
          {
	    std::vector<int> new_sgroups;
	    for (size_t i = 0; i < sgroups.size(); i++)
	      if (sgroups[i] != sss)
		new_sgroups.push_back(sgroups[i]);
	    
	    if (!new_sgroups.empty())
	      {
		(*atomIt)->setProp("_SGroup",new_sgroups);
	      }
	    else
	      {
		(*atomIt)->clearProp("_SGroup");
	      }
	  }
      if ((*atomIt)->hasProp("_senior"))
	(*atomIt)->clearProp("_senior");
      if ((*atomIt)->hasProp("_junior"))
	(*atomIt)->clearProp("_junior");
    }
}

void SwapSeniorJunior(RWMol *mol)
{
  if (!mol)
    return;
  int senior(-1), junior(-1);
  for(ROMol::AtomIterator atomIt=mol->beginAtoms(); atomIt!=mol->endAtoms();++atomIt)
    {
      if ((*atomIt)->hasProp("_NewStartAtom"))
	{
	  senior = (*atomIt)->getIdx();
	}
      if ((*atomIt)->hasProp("_NewEndAtom"))
	{
	  junior = (*atomIt)->getIdx();
	}
    }
  if (senior >= 0 && junior >= 0)
    {
      mol->getAtomWithIdx(senior)->clearProp("_NewStartAtom");
      mol->getAtomWithIdx(junior)->clearProp("_NewEndAtom");
      mol->getAtomWithIdx(senior)->setProp("_NewEndAtom", true);
      mol->getAtomWithIdx(junior)->setProp("_NewStartAtom", true);
      
      RDKit::Conformer &conf =mol->getConformer();
      std::vector<RDGeom::Point3D>& coords = conf.getPositions();
      RDGeom::Point3D v1 = coords[senior];
    
      RDGeom::Transform3D t1;
      t1.SetRotation(M_PI, RDGeom::Z_Axis);
      MolTransforms::transformMolsAtoms(mol,t1);

      RDGeom::Point3D v2 = coords[junior];
      RDGeom::Transform3D t2;
      t2.SetTranslation(v1 - v2);
      MolTransforms::transformMolsAtoms(mol,t2);
    }
}

void AddStartAndFinishFragments(std::vector<boost::shared_ptr<ROMol> > &frags, RWMol *&mol_left, RWMol *&mol_right, bool keep_wildcard)
{
      
  for (unsigned int i=0; i<frags.size(); i++)
    {
      bool wildcard = false;
      if (frags[i]->getNumAtoms() == 1 && (*frags[i]->beginAtoms())->getAtomicNum() == 0 && !keep_wildcard)
	wildcard = true;
      
      for(ROMol::AtomIterator atomIt=frags[i]->beginAtoms(); atomIt!=frags[i]->endAtoms();++atomIt)
	{
	  if ( (*atomIt)->hasProp("_ConnectedStartAtom"))
	    {
	      if (mol_left)
		{
		  if (!wildcard)
		    mol_left->insertMol(*frags[i]);
		  else
		    {
		      delete mol_left;
		      mol_left = new RWMol(*frags[i]);
		    }
		}
	      else
		mol_left = new RWMol(*frags[i]);
	    }
	  if ( (*atomIt)->hasProp("_ConnectedEndAtom"))
	    {
	      if (mol_right)
		{
		  if (!wildcard)
		    mol_right->insertMol(*frags[i]);
		  else
		    {
		      delete mol_right;
		      mol_right = new RWMol(*frags[i]);
		    }
		}
	      else
		mol_right = new RWMol(*frags[i]);
	    }
	}
    }
}


void MoveBrackets(RWMol *mol1, std::vector<RDGeom::Point3D>& coords, bool first_pass)
{
  if (mol1->hasProp("_SGroupBrackets")) 
    {
      std::vector<std::vector<double> >  sgroups;
      mol1->getProp("_SGroupBrackets",sgroups);
     

      for (int sss = 0; sss < sgroups.size(); sss++)
	{
	  if (sgroups[sss].size() != 8)
	    {
	      if (first_pass)
		std::cerr << "Bracket coordinates malformed for sgroup " << sss+1 << std::endl;
	      continue;
	    }
	  std::vector<unsigned int> a, b;
	  GetConnectingAtomsAndBonds(mol1, b, a, sss+1);
	  
	  if (a.size() != 4)
	    {
	      continue;
	    }
	  unsigned int a1 = a[0];
	  unsigned int a2 = a[1];
	  unsigned int a3 = a[2];
	  unsigned int a4 = a[3];

	  double dx1 =  (coords[a1].x + coords[a2].x)/2 - (sgroups[sss][0] + sgroups[sss][2]) / 2;
	  double dy1 =  (coords[a1].y + coords[a2].y)/2 - (sgroups[sss][1] + sgroups[sss][3]) / 2;
	  double dx2 =  (coords[a3].x + coords[a4].x)/2 - (sgroups[sss][4] + sgroups[sss][6]) / 2;
	  double dy2 =  (coords[a3].y + coords[a4].y)/2 - (sgroups[sss][5] + sgroups[sss][7]) / 2;
	  sgroups[sss][0] += dx1;
	  sgroups[sss][1] += dy1;
	  sgroups[sss][2] += dx1;
	  sgroups[sss][3] += dy1;
	  sgroups[sss][4] += dx2;
	  sgroups[sss][5] += dy2;
	  sgroups[sss][6] += dx2;
	  sgroups[sss][7] += dy2;
	  if ((coords[a1].x + coords[a2].x)/2 > (coords[a3].x + coords[a4].x)/2 )
	    {
	      std::swap( sgroups[sss][0], sgroups[sss][4]);
	      std::swap( sgroups[sss][1], sgroups[sss][5]);
	      std::swap( sgroups[sss][2], sgroups[sss][6]);
	      std::swap( sgroups[sss][3], sgroups[sss][7]);
	    }
	  sgroups[sss][0] += 0.1 * fabs(coords[a1].x - coords[a2].x);
	  sgroups[sss][2] += 0.1 * fabs(coords[a1].x - coords[a2].x);
	  sgroups[sss][4] -= 0.1 * fabs(coords[a3].x - coords[a4].x);
	  sgroups[sss][6] -= 0.1 * fabs(coords[a3].x - coords[a4].x);
	}
      mol1->setProp("_SGroupBrackets",sgroups);
    }
}

void ConnectFragment(RWMol *mol, const std::string &connected_start, const std::string &start, const std::string &new_end, const std::string &new_connected_start,
		     int sss, std::vector<int> &sgroups_bond)
{
  int a1 = -1;
  int a2 = -1;
  for(ROMol::AtomIterator atomIt=mol->beginAtoms(); atomIt!=mol->endAtoms();++atomIt)
    {
      if ( (*atomIt)->hasProp(connected_start))
	{
	  a1 = (*atomIt)->getIdx();
	}
      if ( (*atomIt)->hasProp(start))
	{
	  a2 = (*atomIt)->getIdx();
	}
      if ( (*atomIt)->hasProp(new_end))
	{
	  (*atomIt)->clearProp(new_end);
	  (*atomIt)->setProp(new_connected_start, true);
	}
    }
  if (a1 >= 0 && a2 >= 0)
    {
      std::vector<int> sgroups;
      for (size_t i = 0; i < sgroups_bond.size(); i++)
	if (sgroups_bond[i] != sss)
	  sgroups.push_back(sgroups_bond[i]);
       unsigned int bid = mol->addBond(a1,a2,Bond::SINGLE);
       bid--;
       if (!sgroups.empty())
	 mol->getBondWithIdx(bid)->setProp("_SGroup", sgroups);
       sgroups_bond.clear();
    }
  else
    {
       for(ROMol::AtomIterator atomIt=mol->beginAtoms(); atomIt!=mol->endAtoms();++atomIt)
	 {
	   if ( (*atomIt)->hasProp(connected_start))
	     {
	       (*atomIt)->setProp(new_connected_start, true);
	     }
	 }       
    }
}

void GetConnectionAtoms(ROMol *mol1, int &a1, int &a2, int &a3, int &a4)
{
  for(ROMol::AtomIterator atomIt=mol1->beginAtoms(); atomIt!=mol1->endAtoms();++atomIt)
    {
      if ( (*atomIt)->hasProp("_NewConnectedStartAtom"))
	a1 = (*atomIt)->getIdx();
      if ( (*atomIt)->hasProp("_NewStartAtom"))
	a2 = (*atomIt)->getIdx();
      if ( (*atomIt)->hasProp("_NewEndAtom"))
	a3 = (*atomIt)->getIdx();
      if ( (*atomIt)->hasProp("_NewConnectedEndAtom"))
	a4 = (*atomIt)->getIdx();
    }
}

void CreateBond(RWMol *mol1, int a1, int a2, int sss, const std::vector<int> &sgroups_bond) 
{
  unsigned int bid = mol1->addBond(a1,a2,Bond::SINGLE);
  bid--;
  if (!sgroups_bond.empty())
    {
      std::set<int> sgroups_set(sgroups_bond.begin(), sgroups_bond.end());
      sgroups_set.insert(sss);
      std::vector<int> sgroups(sgroups_set.begin(), sgroups_set.end());
      mol1->getBondWithIdx(bid)->setProp("_SGroup", sgroups);
    }
}

void ClearAtomProp(ROMol *mol, const std::string &prop)
{
  for(ROMol::AtomIterator atomIt=mol->beginAtoms(); atomIt!=mol->endAtoms();++atomIt)
    {
      if ( (*atomIt)->hasProp(prop))
	{
	  (*atomIt)->clearProp(prop);
	}
    }
}

RDGeom::Point3D GetAtomPosition(ROMol *mol, const std::string &prop)
{
  RDKit::Conformer &conf =mol->getConformer();

  std::vector<RDGeom::Point3D> &coords = conf.getPositions();
  unsigned int aid = 0;
  for(ROMol::AtomIterator atomIt=mol->beginAtoms(); atomIt!=mol->endAtoms();++atomIt)
    {
      if ( (*atomIt)->hasProp(prop))
	{
	  aid = (*atomIt)->getIdx();
	  break;
	}
    }
  return coords[aid];
}

bool comp_by_inchi( const std::pair<int,std::string> &a, const std::pair<int,std::string> &b)
{
  return(a.second > b.second);
}

void LocateThirdTailAtom(RWMol &mol)
{
  for(ROMol::AtomIterator at=mol.beginAtoms();at!=mol.endAtoms();++at)
    {
      if ((*at)->getAtomicNum() != 0)	
	continue;
      std::vector<int> sgroups1, sgroups2;
      if (!(*at)->getPropIfPresent("_SGroup", sgroups1))
	continue;
      int num = 0;
      ROMol::ADJ_ITER begin, end;
      boost::tie(begin,end) = mol.getAtomNeighbors(*at);
      while(begin!=end)
	{
	  sgroups2.clear();
	  mol.getAtomWithIdx(*begin)->getPropIfPresent("_SGroup", sgroups2);
	  ++num;
	  ++begin;
	}
      if (num != 1)
	continue;
      std::sort(sgroups1.begin(), sgroups1.end());
      std::sort(sgroups2.begin(), sgroups2.end());
      if (sgroups1 != sgroups2)
	continue;
      (*at)->setProp("_branched",true);
    }
}

bool IsSruSalt(ROMol *mol)
{
  std::set<int> sss_set;
  FindAtomSGroups(mol, sss_set);
  for (std::set<int>::iterator sss_it = sss_set.begin(); sss_it != sss_set.end(); ++sss_it)
    {
      std::vector<unsigned int> bonds;
      std::vector<unsigned int> atoms;
      int sss = *sss_it;
      GetConnectingAtomsAndBonds(mol, bonds, atoms, sss);
      if (bonds.empty() && atoms.empty())
	{
	  mol->setProp("_sru_salt", sss);
	  return true;
	}
    }
  return false;
}
       
bool ProcessMolFragment(RWMol *&mol, bool print_connection, int num_atoms, const std::string &output_sru, bool keep_wildcard, bool canonicalize, bool branched)
{
  bool result = true;
  std::set<int> sss_set;
  FindAtomSGroups(mol, sss_set);

  if (branched)
    LocateThirdTailAtom(*mol);
  
  if (sss_set.empty())
    return true;
  
  MarkOldAtomIds(mol);
  
  std::set<int> connections_set;
  std::vector<std::pair<int,std::string> > sorted_sss;
  std::map<std::string, std::set<std::string> > inchi_to_label;
  for (std::set<int>::iterator sss_it = sss_set.begin(); sss_it != sss_set.end(); ++sss_it)
    {
      std::vector<unsigned int> bonds;
      std::vector<unsigned int> atoms;
      int sss = *sss_it;

      RotateMolToBrackets(mol, sss - 1);

      GetConnectingAtomsAndBonds(mol, bonds, atoms, sss);

      if (bonds.size() > 2)
	{
	  continue; // TODO Non-linear SRU?
	}
      
      if (bonds.size() != 2 || atoms.size() > 2)
	{
	  std::cerr << "Incorrect number of SRUs for sgroup " << sss << " " << bonds.size() << " " << atoms.size() << std::endl;
	  //std::string mb=MolToMolBlock(*mol);
	  //std::cout << mb;
	  result = false;
	  break;
	}

      if (bonds.size() != 2 || sss < 0 || atoms.size() != 2 || bonds.front() == bonds.back() || atoms.front() == atoms.back())
	{
	  std::cerr << "Cannot parse SGroup information for " << sss << std::endl;
	  result = false;
	  break;
	}
      if (print_connection && atoms.size() == 2)
	{
	  std::cout << atoms.front()+1 + num_atoms<< " " << atoms.back()+1 + num_atoms << std::endl;
	}
      const Bond *bond1 = mol->getBondWithIdx (bonds.front());
      const Bond *bond2 = mol->getBondWithIdx (bonds.back());
      std::vector<int> sgroups_bond1;
      std::vector<int> sgroups_bond2;      
      MarkConnectingAtoms(mol, bond1, bond2, atoms.front(), atoms.back(), sgroups_bond1, sgroups_bond2);
      
      unsigned int aid1, aid2;
      int fragment = -1;
      std::vector<boost::shared_ptr<ROMol> > frags;
      RWMol *mol1 = GetSRU(mol, aid1, aid2, fragment, frags, bond1, bond2);     

      if (!mol1)
	{
	  std::cerr << "Cannot extract SRU" << std::endl;
	  result = false;
	  break;
	}
      
      std::vector<std::pair<unsigned int, unsigned int> > connections;	 
      FindBreakableBonds(mol1, aid1, aid2, connections);
      if (BelongsToMultipleSgroups(mol1, connections_set, connections))
	{
	  delete mol1;
	  result = false;
	  break;
	}
      
      if (!canonicalize)
	connections.clear();
      
      unsigned int min_a1;
      unsigned int min_a2;
      unsigned int min_senior;
      unsigned int min_junior;
      std::string min_inchi = FindMinimumInChI(mol1, aid1, aid2, connections, min_a1, min_a2, min_senior, min_junior);
      if (min_inchi.empty())
	{
	  delete mol1;
	  result = false;
	  break;
	}
      {
	sorted_sss.push_back(make_pair(sss,min_inchi));
	std::vector<std::string> sgroup;
	mol1->getPropIfPresent("_SGroupSubscript",sgroup);
	if (sss - 1 < sgroup.size() && !sgroup[sss - 1].empty())
	  inchi_to_label[min_inchi].insert(sgroup[sss - 1]);
      }
      delete mol1;

      frags[fragment]->getAtomWithIdx(min_senior)->setProp("_senior",true);
      frags[fragment]->getAtomWithIdx(min_junior)->setProp("_junior",true);
      
      RWMol *mol_left = NULL, *mol_right = NULL;
      double d1_length = 0, d2_length = 0;
      GetLeftRightCenterFragments(frags[fragment], mol, min_a1, min_a2, mol1, mol_left, mol_right, d1_length, d2_length);   

      ClearAtomSgroup(mol_left, sss);	  
      ClearAtomSgroup(mol_right, sss);
      
      AddStartAndFinishFragments(frags, mol_left, mol_right, keep_wildcard);
     
      ConnectFragment(mol_left, "_ConnectedStartAtom", "_StartAtom", "_NewEndAtom", "_NewConnectedStartAtom", sss, sgroups_bond1);
      ConnectFragment(mol_right, "_ConnectedEndAtom", "_EndAtom", "_NewStartAtom", "_NewConnectedEndAtom", sss, sgroups_bond2);                

      if (d1_length != 0 && d2_length != 0)
	{

	  RDGeom::Point3D v_left = GetAtomPosition(mol_left, "_NewConnectedStartAtom");
	  RDGeom::Point3D v_right = GetAtomPosition(mol_right, "_NewConnectedEndAtom");  
	  
	  RDGeom::Point3D v1 = GetAtomPosition(mol1, "_NewStartAtom");
	  RDGeom::Point3D v2 = GetAtomPosition(mol1, "_NewEndAtom");
      
	  int sign = 1;
	  if (v1.x > v2.x)
	    sign  = -1;
	  v1.x -= sign * d1_length;
	  v2.x += sign * d2_length;
	  v1.y += 0.3 * d1_length;
	  v2.y += 0.3 * d2_length;
      
	  RDGeom::Transform3D t_left;
	  t_left.SetTranslation(v1 - v_left);
	  MolTransforms::transformMolsAtoms(mol_left,t_left); 
      
	  RDGeom::Transform3D t_right;
	  t_right.SetTranslation(v2 - v_right);
	  MolTransforms::transformMolsAtoms(mol_right,t_right); 
	}
      
      mol1->insertMol(*mol_left); 
      mol1->insertMol(*mol_right);      

      int a1 = -1;
      int a2 = -1;
      int a3 = -1;
      int a4 = -1;
      GetConnectionAtoms(mol1, a1, a2, a3, a4);
      if (a1 < 0 || a2 < 0 || a3 < 0 || a4 < 0)
	{
	  std::cerr << "Cannot determine connection atoms" << std::endl;
	  //std::cerr << a1 <<" "<<a2<<" "<<a3<<" "<<a4<<std::endl;
	  delete mol_left;
	  delete mol_right;
	  result = false;
	  break;
	}
      
      CreateBond(mol1, a1, a2, sss, sgroups_bond1); 
      CreateBond(mol1, a3, a4, sss, sgroups_bond2); 
    
      delete mol_left;
      delete mol_right;
      
      ClearAtomProp(mol1,"_StartAtom");
      ClearAtomProp(mol1,"_EndAtom");
      ClearAtomProp(mol1,"_NewStartAtom");
      ClearAtomProp(mol1,"_NewEndAtom");
      ClearAtomProp(mol1,"_ConnectedStartAtom");
      ClearAtomProp(mol1,"_NewConnectedStartAtom");
      ClearAtomProp(mol1,"_ConnectedEndAtom");
      ClearAtomProp(mol1,"_NewConnectedEndAtom");

      delete mol;
      mol = mol1;
    }
 
  ClearOldAtomIds(mol);
  if (!result)
    {
      return result;
    }
  
  std::sort(sorted_sss.begin(), sorted_sss.end(),comp_by_inchi);

  bool first_pass = true;
  SDWriter *writer = NULL;
  if (!output_sru.empty())
    writer = new SDWriter(output_sru);
  std::set<std::string> seen_inchi;
  for (std::vector<std::pair<int,std::string> >::iterator sss_it = sorted_sss.begin(); sss_it != sorted_sss.end(); ++sss_it)
    {
     
      std::vector<unsigned int> bonds;
      std::vector<unsigned int> atoms;
      int sss = sss_it->first;

      RotateMolToBrackets(mol, sss - 1);

      GetConnectingAtomsAndBonds(mol, bonds, atoms, sss); 

      if (bonds.size() > 2)
	{
	  continue; // TODO Non-linear SRU?
	}
       
      if (bonds.size() != 2 || atoms.size() > 2)
	{
	  result = false;
	  break;
	}

      if (bonds.size() != 2 || sss < 0 || atoms.size() != 2 || bonds.front() == bonds.back() || atoms.front() == atoms.back())
	{
	  result = false;
	  break;
	}

      const Bond *bond1 = mol->getBondWithIdx (bonds.front());
      const Bond *bond2 = mol->getBondWithIdx (bonds.back());
      std::vector<int> sgroups_bond1;
      std::vector<int> sgroups_bond2;      
      MarkConnectingAtoms(mol, bond1, bond2, atoms.front(), atoms.back(), sgroups_bond1, sgroups_bond2);
      
      unsigned int aid1, aid2;
      int fragment = -1;
      std::vector<boost::shared_ptr<ROMol> > frags;
      RWMol *mol1 = GetSRU(mol, aid1, aid2, fragment, frags, bond1, bond2);     
     
      if (!mol1)
	{
	  result = false;
	  break;
	}
      std::string min_inchi = GetMinimumInChI(mol1, aid1, aid2);
      if (min_inchi.empty())
	{
	  delete mol1;
	  result = false;
	  break;
	}
      unsigned int min_a1 = aid1;
      unsigned int min_a2 = aid2;
      delete mol1;

      RWMol *mol_left = NULL, *mol_right = NULL;
      double d1_length = 0, d2_length = 0;
      GetLeftRightCenterFragments(frags[fragment], mol, min_a1, min_a2, mol1, mol_left, mol_right, d1_length, d2_length);
      
      if (writer)
	{
	  OutputSRU(writer,mol1, min_inchi, seen_inchi, inchi_to_label);
	}
      
      ClearAtomSgroup(mol_left, sss);	  
      ClearAtomSgroup(mol_right, sss);
      
      AddStartAndFinishFragments(frags, mol_left, mol_right, keep_wildcard);
     
      ConnectFragment(mol_left, "_ConnectedStartAtom", "_StartAtom", "_NewEndAtom", "_NewConnectedStartAtom", sss, sgroups_bond1);
      ConnectFragment(mol_right, "_ConnectedEndAtom", "_EndAtom", "_NewStartAtom", "_NewConnectedEndAtom", sss, sgroups_bond2);                
      
      RDGeom::Point3D v_left_old = GetAtomPosition(mol_left, "_NewConnectedStartAtom");
      RDGeom::Point3D v_right_old = GetAtomPosition(mol_right, "_NewConnectedEndAtom");
	  
      bool removed = true;
      bool something_was_removed = false;
      bool partial_removed = false;
      while (removed)
	{
	  removed = false;
	  removed |= RemoveSRU(mol_left, mol1,"_NewConnectedStartAtom", sgroups_bond1); 
	  removed |= RemoveSRU(mol_right, mol1,"_NewConnectedEndAtom",  sgroups_bond2);
	  bool partial = RemovePartialSRU(mol_left, mol_right, mol1, sgroups_bond1, sgroups_bond2);
	  partial_removed |= partial;
	  removed |= partial;	  
	  something_was_removed |= removed;
	}

      if (something_was_removed)
      	{
	  RDGeom::Point3D v_left_new = GetAtomPosition(mol_left, "_NewConnectedStartAtom");
	  RDGeom::Point3D v_right_new = GetAtomPosition(mol_right, "_NewConnectedEndAtom");
      
	  RDGeom::Transform3D t_left;
	  t_left.SetTranslation(v_left_old - v_left_new);
	  MolTransforms::transformMolsAtoms(mol_left,t_left); 
      
	  RDGeom::Transform3D t_right;
	  t_right.SetTranslation(v_right_old - v_right_new);
	  MolTransforms::transformMolsAtoms(mol_right,t_right); 
	}
      if (partial_removed)
	{
	  SwapSeniorJunior(mol1);
	}
	
      mol1->insertMol(*mol_left); 
      mol1->insertMol(*mol_right);      

      int a1 = -1;
      int a2 = -1;
      int a3 = -1;
      int a4 = -1;
      GetConnectionAtoms(mol1, a1, a2, a3, a4);
      if (a1 < 0 || a2 < 0 || a3 < 0 || a4 < 0)
	{
	  delete mol_left;
	  delete mol_right;
	  result = false;
	  break;
	}
      
      CreateBond(mol1, a1, a2, sss, sgroups_bond1); 
      CreateBond(mol1, a3, a4, sss, sgroups_bond2); 
    
      delete mol_left;
      delete mol_right;
      
      RDKit::Conformer &conf =mol1->getConformer();
      std::vector<RDGeom::Point3D> &coords = conf.getPositions();

      MoveBrackets(mol1, coords, first_pass); 

      ClearAtomProp(mol1,"_StartAtom");
      ClearAtomProp(mol1,"_EndAtom");
      ClearAtomProp(mol1,"_NewStartAtom");
      ClearAtomProp(mol1,"_NewEndAtom");
      ClearAtomProp(mol1,"_ConnectedStartAtom");
      ClearAtomProp(mol1,"_NewConnectedStartAtom");
      ClearAtomProp(mol1,"_ConnectedEndAtom");
      ClearAtomProp(mol1,"_NewConnectedEndAtom");

      delete mol;
      mol = mol1;
      first_pass = false;
    }
  
  if (writer)
    {
      writer->flush();
      writer->close();
      delete writer;
    }
  
  return result;
}

void parse_xml(std::istream& stream)
{
  pugi::xml_document doc;
  if (!doc.load(stream))
    {
      std::cerr<<"Cannot parse input xml"<<std::endl;
      return;
    }
  
  pugi::xml_node polymer = doc.child("POLYMER").child("STRUCTURAL_REPEAT_UNIT");
  for (pugi::xml_node sru = polymer.child("STRUCTURAL_REPEAT_UNIT_GROUP"); sru; sru = sru.next_sibling("STRUCTURAL_REPEAT_UNIT_GROUP"))
    if (!sru.empty())
      {
	std::string conn = sru.child_value("FRAGMENT_CONNECTIVITY");
      }
}

void CheckConsistency(ROMol *mol)
{
  std::set<int> sss_set;
  FindBondSGroups(mol, sss_set);
  if (sss_set.empty())
    {
      std::cerr << "M  SBL block not found or incorrect" << std::endl; 
    }
  int total = 0;
  int sru = 0;
  for(ROMol::AtomIterator atomIt=mol->beginAtoms(); atomIt!=mol->endAtoms(); ++atomIt)
    {
      total++;
      std::vector<int> sgroups;
      if ((*atomIt)->getPropIfPresent("_SGroup", sgroups))
	{
	  if (!sgroups.empty()) 
	    {
	      sru++;
	    }
	}
    }
  if (total == sru && total != 0)
    {
      std::cerr << "All atoms a part of SRU, malformed M SAL block?" << std::endl;
    }
  if (total == 0)
    {
      std::cerr << "No atoms found" << std::endl;
    }
  if (sru == 0 && total != 0)
    {
      std::cerr << "No SRU found, malformed M SAL block?" << std::endl;
    }
}

void AddImplicitHydrogens(RWMol &mol, bool addCoords)
{
  for (ROMol::AtomIterator ai = mol.beginAtoms(); ai != mol.endAtoms(); ++ai)
    {
      (*ai)->calcImplicitValence(false);
    }
  MolOps::setHybridization(mol);
  mol.clearComputedProps(false);

  unsigned int numAddHyds = 0;
  for(ROMol::AtomIterator at=mol.beginAtoms();at!=mol.endAtoms();++at)
    {
      if ((*at)->getAtomicNum() == 6 || (*at)->getAtomicNum() == 0)
	continue;
      numAddHyds += (*at)->getNumExplicitHs();
      numAddHyds += (*at)->getNumImplicitHs();
    }
  unsigned int nSize = mol.getNumAtoms() + numAddHyds;
  
  for (ROMol::ConformerIterator cfi = mol.beginConformers(); cfi != mol.endConformers(); ++cfi)
    {
      (*cfi)->reserve(nSize);
    }

  unsigned int stopIdx=mol.getNumAtoms();
  for(unsigned int aidx=0;aidx<stopIdx;++aidx)
    {
      Atom *newAt=mol.getAtomWithIdx(aidx);
      if (newAt->getAtomicNum() == 6 || newAt->getAtomicNum() == 0)
	continue;
      unsigned int newIdx;
      newAt->clearComputedProps();
      unsigned int onumexpl=newAt->getNumExplicitHs();
      for(unsigned int i=0;i<onumexpl;i++)
	{
          newIdx=mol.addAtom(new Atom(1),false,true);
          mol.addBond(aidx,newIdx,Bond::SINGLE);	 
          mol.getAtomWithIdx(newIdx)->updatePropertyCache();
          if(addCoords)
	    MolOps::setHydrogenCoords(&mol,newIdx,aidx);
	  std::vector<int> sgroups;
	  if (newAt->getPropIfPresent("_SGroup", sgroups))
	    mol.getAtomWithIdx(newIdx)->setProp("_SGroup", sgroups);
        }
      newAt->setNumExplicitHs(0);
      
      for(unsigned int i=0;i<mol.getAtomWithIdx(aidx)->getNumImplicitHs();i++)
	{
	  newIdx=mol.addAtom(new Atom(1),false,true);
	  mol.addBond(aidx,newIdx,Bond::SINGLE);	 
	  mol.getAtomWithIdx(newIdx)->setProp(common_properties::isImplicit,1);
	  mol.getAtomWithIdx(newIdx)->updatePropertyCache();
	  if(addCoords)
	    MolOps::setHydrogenCoords(&mol,newIdx,aidx);
	  std::vector<int> sgroups;
	  if (newAt->getPropIfPresent("_SGroup", sgroups))
	    mol.getAtomWithIdx(newIdx)->setProp("_SGroup", sgroups);
	}

      newAt->setProp(common_properties::origNoImplicit,newAt->getNoImplicit(), true);
      newAt->setNoImplicit(true);
      try {
	newAt->updatePropertyCache();
      } catch(...) {}
    }
}

ROMol* AddImplicitHydrogens(const ROMol &mol, bool addCoords)
{
  RWMol *res = new RWMol(mol);
  AddImplicitHydrogens(*res, addCoords);
  return static_cast<ROMol *>(res);
}

bool process_sdf( const std::string &fname, const std::string &ofile, bool print_connection, const std::string &output_sru,
		  bool keep_wildcard, bool split_fragments, bool canonicalize, bool branched)
{
  bool err = false;
  SDMolSupplier sdsup(fname, false, false, false);  
  SDWriter *writer = new SDWriter(ofile);
  int polymer_label = 1;
  while (!sdsup.atEnd()) 
    {
      ROMol *old_mol = sdsup.next();
      if (old_mol)
	{
	  std::vector<boost::shared_ptr<ROMol> > separate_fragments;

	  std::stringstream xml;
	  for (int i = 1; i < 10; i++)
	    {
	      std::stringstream ss;
	      ss << "DESC_PART" << i;
	      if (old_mol->hasProp(ss.str()))
		    {
		      std::string part;
		      old_mol->getProp(ss.str(),part);
		      xml << part;
		    }
	      else
		    break;
	    }
	  parse_xml(xml);

	  bool is_chiral_flag = false;
	  unsigned int chirality_flag(0);
	  if (old_mol->hasProp("_MolFileChiralFlag"))
	    {
	      old_mol->getProp("_MolFileChiralFlag",chirality_flag);
	      is_chiral_flag = true;
	    }
	  CheckConsistency(old_mol);
	  ROMol* old_mol_h = AddImplicitHydrogens(*old_mol, true);
	  std::vector<boost::shared_ptr<ROMol> > frags, salts;
	  {
	    std::vector<boost::shared_ptr<ROMol> > frags_tmp = MolOps::getMolFrags(*old_mol_h,false,NULL);
	    for (auto f : frags_tmp)
	      if (IsSruSalt(f.get()))
		salts.push_back(f);
	      else
		frags.push_back(f);
	  }
	  RWMol *new_mol = NULL;
	  int num_atoms = 0;
	  for (size_t i=0; i<frags.size(); i++)
	    {
	      RWMol *mol(new RWMol(*(frags[i])));
	      if (!ProcessMolFragment(mol, print_connection, num_atoms, output_sru, keep_wildcard, canonicalize, branched))
		{
		  delete mol;
		  mol = new RWMol(*(frags[i]));
		  err = true;
		}	      	      
	      
	      if (!err && split_fragments)
		{
		  std::vector<boost::shared_ptr<ROMol> > single_mol_fragments;
		  SplitFragments(mol, single_mol_fragments, is_chiral_flag, chirality_flag, keep_wildcard, polymer_label, salts);
		  separate_fragments.insert(separate_fragments.end(), single_mol_fragments.begin(), single_mol_fragments.end());
		  polymer_label++;
		}
	      
	      if (!new_mol)
		new_mol = new RWMol(*mol);
	      else
		{
		  std::set<int> sss_set;
		  FindAtomSGroups(mol, sss_set);
		  std::vector<std::vector<double> >  sgroups;
		  SaveBrackets(mol, sgroups);
		  new_mol->insertMol(*mol);
		  RestoreBrackets(new_mol, sgroups, sss_set);	  
		}
	      	      
	      delete mol;
	      num_atoms += frags[i]->getNumAtoms();
	    }
	  if (new_mol)
	    {
	      if (is_chiral_flag)
		{
		  new_mol->setProp("_MolFileChiralFlag",chirality_flag);
		}
	      if (!split_fragments)
		writer->write(*new_mol);
	      delete new_mol;
	    }
	  else
	    {
	      if (!split_fragments)
		writer->write(*old_mol);
	    }
	  if (split_fragments)
	    {
	      for (size_t i = 0; i < separate_fragments.size(); i++)
		{
		  if (is_chiral_flag)
		    {
		      separate_fragments[i]->setProp("_MolFileChiralFlag",chirality_flag);
		    }
		  writer->write(*separate_fragments[i]);
		}
	    }
	  delete old_mol;
	  delete old_mol_h;
	}	   
    }
  writer->flush();
  writer->close();
  delete writer;
  return err;
}

bool process_mol(const std::string &fname, const std::string &xml, const std::string &out, bool print_connection, const std::string &output_sru,
		 bool keep_wildcard, bool split_fragments, bool canonicalize, bool branched)
{
  bool err = false;
  if (!xml.empty())
    {
      std::ifstream stream(xml.c_str());
      parse_xml(stream);
    }

  std::ofstream ostr(out.c_str());
  int polymer_label = 1;
  RWMol *old_mol = MolFileToMol(fname,false, false, false);  
  std::vector<boost::shared_ptr<ROMol> > separate_fragments;
  
  bool is_chiral_flag = false;
  unsigned int chirality_flag(0);
  if (old_mol->hasProp("_MolFileChiralFlag"))
    {
      old_mol->getProp("_MolFileChiralFlag",chirality_flag);
      is_chiral_flag = true;
    }
  CheckConsistency(old_mol);
  AddImplicitHydrogens(*old_mol, true);
  std::vector<boost::shared_ptr<ROMol> > frags, salts;
  {
    std::vector<boost::shared_ptr<ROMol> > frags_tmp = MolOps::getMolFrags(*old_mol,false,NULL);
    for (auto f : frags_tmp)
      if (IsSruSalt(f.get()))
	salts.push_back(f);
      else
	frags.push_back(f);
  }
	  
  RWMol *new_mol = NULL;
  int num_atoms = 0;
  for (size_t i=0; i<frags.size(); i++)
    {
      RWMol *mol(new RWMol(*(frags[i])));
      if (!ProcessMolFragment(mol, print_connection, num_atoms, output_sru, keep_wildcard, canonicalize, branched))
	{
	  delete mol;
	  mol = new RWMol(*(frags[i]));
	  err = true;
	}

      if (!err && split_fragments)
	{
	  std::vector<boost::shared_ptr<ROMol> > single_mol_fragments;
	  SplitFragments(mol, single_mol_fragments, is_chiral_flag, chirality_flag, keep_wildcard, polymer_label, salts);
	  separate_fragments.insert(separate_fragments.end(), single_mol_fragments.begin(), single_mol_fragments.end());
	  polymer_label++;
	}
      
      if (!new_mol)
	new_mol = new RWMol(*mol);
      else
	{
	  std::set<int> sss_set;
	  FindAtomSGroups(mol, sss_set);
	  std::vector<std::vector<double> >  sgroups;
	  SaveBrackets(mol, sgroups);
	  new_mol->insertMol(*mol);
	  RestoreBrackets(new_mol, sgroups, sss_set);	  
	}
     
      delete mol;
      num_atoms += frags[i]->getNumAtoms();
    }
  
  std::string mb=MolToMolBlock(*old_mol);
  if (new_mol)
    {
      if (is_chiral_flag)
	{
	  new_mol->setProp("_MolFileChiralFlag",chirality_flag);
	}

      mb=MolToMolBlock(*new_mol);
      delete new_mol;
    }

  if (!split_fragments)
    {
      ostr << mb;
    }
  else
    {
      SDWriter *writer = new SDWriter(&ostr);
      for (size_t i = 0; i < separate_fragments.size(); i++)
	{
	  if (is_chiral_flag)
	    {
	      separate_fragments[i]->setProp("_MolFileChiralFlag",chirality_flag);
	    }
	  writer->write(*separate_fragments[i]);
	}
      writer->flush();
      writer->close();
      delete writer;
    }

  delete old_mol;
  return err;
}

bool RemovePartialSRU(RWMol *mol_left, RWMol *mol_right, RWMol *sru, std::vector<int> &sgroups_bond1, std::vector<int> &sgroups_bond2)
{
  bool found = false;
  int aid1 = -1;
  int aid2 = -1;
  for(ROMol::AtomIterator atomIt=sru->beginAtoms(); atomIt!=sru->endAtoms();++atomIt)
    {
      if ( (*atomIt)->hasProp("_NewStartAtom"))
	{
	  aid1 = (*atomIt)->getIdx();
	}
      if ( (*atomIt)->hasProp("_NewEndAtom"))
	{
	  aid2 = (*atomIt)->getIdx();
	}
    }
  if (aid1 < 0 || aid2 < 0)
    return false;

  std::string orig_inchi;
  std::string aux;
  RDKit::ExtraInchiReturnValues rv;
  try
    {
      orig_inchi = MolTextToInchi(*sru, rv, "/NPZz");
    } catch(...) {}
  if (orig_inchi.empty())
    return false;

  int min_atoms = INT_MAX;
  boost::shared_ptr<ROMol> sru_left, sru_right;
  std::vector<std::pair<unsigned int, unsigned int> > connections;	 
  FindBreakableBonds(sru, aid1, aid2, connections);
  for (size_t i = 0; i < connections.size(); i++)
    {
      unsigned int a1 = connections[i].first;
      unsigned int a2 = connections[i].second;
      RWMol* mol1 = new RWMol(*sru); 
      mol1->removeBond(a1, a2);
      std::vector<boost::shared_ptr<ROMol> > frags = MolOps::getMolFrags(*mol1,false,NULL);
      mol1->addBond(aid1, aid2, Bond::SINGLE);
      std::string inchi;
      try
	{
	  inchi = MolTextToInchi(*mol1, rv, "/NPZz");
	} catch(...) {}      
      delete mol1;
      if (inchi != orig_inchi)
	continue;
      boost::shared_ptr<ROMol> tmp_sru_left, tmp_sru_right;
      for (unsigned int j=0; j<frags.size(); j++)
	{
	  for(ROMol::AtomIterator atomIt=frags[j]->beginAtoms(); atomIt!=frags[j]->endAtoms();++atomIt)
	    {
	      if ( (*atomIt)->hasProp("_NewStartAtom"))
		{
		  tmp_sru_left = frags[j];
		}
	      if ( (*atomIt)->hasProp("_NewEndAtom"))
		{
		  tmp_sru_right = frags[j];
		}
	    }
	}
      
      if (!tmp_sru_left || !tmp_sru_right)
	continue;
      RWMol* tmp_mol_left = new RWMol(*mol_left);
      RWMol* tmp_mol_right = new RWMol(*mol_right); 
      std::vector<int> tmp_bond;
      bool found1 = RemoveSRU(tmp_mol_left, tmp_sru_right.get(),"_NewConnectedStartAtom", tmp_bond); 
      bool found2 = RemoveSRU(tmp_mol_right, tmp_sru_left.get(),"_NewConnectedEndAtom",  tmp_bond);
      if (found1 && found2 && (tmp_mol_left->getNumAtoms() + tmp_mol_right->getNumAtoms()) < min_atoms)
	{
	  sru_left = tmp_sru_left;
	  sru_right = tmp_sru_right;
	}
      delete tmp_mol_left;
      delete tmp_mol_right;
    }
  if (sru_left && sru_right)
    {
      found |= RemoveSRU(mol_left, sru_right.get(),"_NewConnectedStartAtom", sgroups_bond1);
      found |= RemoveSRU(mol_right, sru_left.get(),"_NewConnectedEndAtom",  sgroups_bond2);
    }
  
  return found;
}

bool RemoveSRU(RWMol *mol, ROMol *sru, const std::string &prop, std::vector<int> &sgroups_bond)
{
  std::set<int> sss_set;
  for(ROMol::AtomIterator atomIt=sru->beginAtoms(); atomIt!=sru->endAtoms(); ++atomIt)
    {
      std::vector<int> sgroups;
      if ((*atomIt)->getPropIfPresent("_SGroup", sgroups))
	{
	  if (!sgroups.empty()) 
	    {
	      sss_set.insert(sgroups.begin(), sgroups.end());
	    }
	}
    }

  // typedef std::vector< std::pair< int, int > > RDKit::MatchVectType
  // used to return matches from substructure searching, The format is (queryAtomIdx, molAtomIdx) 
  std::vector< MatchVectType > mv;
  
  bool found = false;
  SubstructMatch(*mol, *sru, mv);
  for (size_t i=0; i<mv.size(); i++)
    {
      std::set<int> removed;
      unsigned int degree_sru = 0;
      unsigned int degree = 0;
      for (size_t j = 0; j < mv[i].size(); j++)
	{
	  int aid = mv[i][j].second;
	  const Atom *atom = mol->getAtomWithIdx(aid);
	  int aid_sru = mv[i][j].first;
	  const Atom *atom_sru = sru->getAtomWithIdx(aid_sru);
	  degree_sru += atom_sru->getDegree();
	  degree += atom->getDegree();

	  bool other_sru(false);
	  std::vector<int> sgroups;
	  atom->getPropIfPresent("_SGroup", sgroups);
	  for (size_t k = 0; k < sgroups.size(); k++)
	    if (sss_set.find(sgroups[k]) == sss_set.end())
	      {
		other_sru = true;
		break;
	      }
	      
	  if (other_sru)
	    {
	      found = false;
	      removed.clear();
	      break;
	    }
	  removed.insert(aid);
	  if ( atom->hasProp(prop))
	    found = true;
	}
      if (degree != degree_sru + 1)
	{
	  found  = false;
	  removed.clear();
	}
      if (found)
	{
	  int num = 0;
	  for(ROMol::BondIterator bondIt=mol->beginBonds(); bondIt!=mol->endBonds();++bondIt)
	    {
	      unsigned int aid1 = (*bondIt)->getBeginAtomIdx();
	      unsigned int aid2 = (*bondIt)->getEndAtomIdx();

	      if (removed.find(aid1) != removed.end() && removed.find(aid2) == removed.end())
		{
		  num++;
		}
	      if (removed.find(aid1) == removed.end() && removed.find(aid2) != removed.end())
		{
		  num++;
		}
	    }
	  if (num != 1)
	    {
	      found = false;
	      //std::cerr << "Unexpected SRU match found" << std::endl;
	    }
	  else
	    {
	      for(ROMol::BondIterator bondIt=mol->beginBonds(); bondIt!=mol->endBonds();++bondIt)
		{
		  unsigned int aid1 = (*bondIt)->getBeginAtomIdx();
		  unsigned int aid2 = (*bondIt)->getEndAtomIdx();
		  bool found_bond = false;
		  if (removed.find(aid1) != removed.end() && removed.find(aid2) == removed.end())
		    {
		      mol->getAtomWithIdx(aid2)->setProp(prop, true);
		      found_bond = true;
		    }
		  if (removed.find(aid1) == removed.end() && removed.find(aid2) != removed.end())
		    {
		      mol->getAtomWithIdx(aid1)->setProp(prop,true);
		      found_bond = true;
		    }
		  if (found_bond)
		    {
		      sgroups_bond.clear();
		      (*bondIt)->getPropIfPresent("_SGroup", sgroups_bond);
		    }
		}
	      
	      for (std::set<int>::reverse_iterator rit=removed.rbegin(); rit != removed.rend(); ++rit)
		{
		  mol->removeAtom(*rit);
		}
	    }
	  break;
	}
    }
  return found;
}

void RotateMolToBrackets(RWMol *mol, int sss)
{
  const double eps = 0.01;

  if (!mol->hasProp("_SGroupBrackets"))
    return;

  std::vector<std::vector<double> >  sgroups;
  mol->getProp("_SGroupBrackets",sgroups);
  if (sss >= sgroups.size())
    return;
 
  if (sgroups[sss].size() != 8)
	return;
  double x1 = sgroups[sss][0];
  double y1 = sgroups[sss][1];
  double x2 = sgroups[sss][2];
  double y2 = sgroups[sss][3];
  double x3 = sgroups[sss][4];
  double y3 = sgroups[sss][5];
  double x4 = sgroups[sss][6];
  double y4 = sgroups[sss][7];
    
       
  double px1 = (x1 + x2) / 2;
  double py1 = (y1 + y2) / 2;
  double px2 = (x3 + x4) / 2;
  double py2 = (y3 + y4) / 2;

  if (fabs(py1 - py2) < eps)
    return;
  
  RDKit::Conformer &conf =mol->getConformer();
  std::vector<RDGeom::Point3D>& coords = conf.getPositions();
  RDGeom::Point3D v1(px1,py1,0);
  RDGeom::Point3D v2(px2,py2,0);
  RDGeom::Point3D v = v2 - v1;
  
  double angle = v.signedAngleTo(RDGeom::Point3D(1,0,0));
  
  RDGeom::Transform3D t1,t2;
  t1.SetTranslation(-v1);
  t2.SetRotation(angle, RDGeom::Z_Axis);
  t2 *= t1; 
  MolTransforms::transformMolsAtoms(mol,t2);
  for (size_t i = 0; i < sgroups.size(); i++)
    {
      if (sgroups[i].size() != 8)
	continue;
       double x1 = sgroups[i][0];
       double y1 = sgroups[i][1];
       double x2 = sgroups[i][2];
       double y2 = sgroups[i][3];
       double x3 = sgroups[i][4];
       double y3 = sgroups[i][5];
       double x4 = sgroups[i][6];
       double y4 = sgroups[i][7];       
       RDGeom::Point3D v1(x1,y1,0);
       RDGeom::Point3D v2(x2,y2,0);
       RDGeom::Point3D v3(x3,y3,0);
       RDGeom::Point3D v4(x4,y4,0);
       t2.TransformPoint(v1);
       t2.TransformPoint(v2);
       t2.TransformPoint(v3);
       t2.TransformPoint(v4);
       sgroups[i][0] = v1.x;
       sgroups[i][1] = v1.y;
       sgroups[i][2] = v2.x;
       sgroups[i][3] = v2.y;
       sgroups[i][4] = v3.x;
       sgroups[i][5] = v3.y;
       sgroups[i][6] = v4.x;
       sgroups[i][7] = v4.y;
    }
  mol->setProp("_SGroupBrackets",sgroups);
}

void OutputSRU(SDWriter *writer, RWMol *mol, const std::string &min_inchi, std::set<std::string> &seen_inchi,
	       const std::map<std::string, std::set<std::string> > &inchi_to_label)
{
  if (seen_inchi.find(min_inchi) == seen_inchi.end())
    {
      RWMol *mol1 = new RWMol(*mol);

      RemoveAllSGroups(mol1);
      std::map<std::string, std::set<std::string> >::const_iterator labels = inchi_to_label.find(min_inchi);
      if (labels != inchi_to_label.end())
	{
	  std::stringstream str;
	  bool first = true;
	  for (std::set<std::string>::const_iterator l = labels->second.begin(); l != labels->second.end(); ++l)
	    {
	      if (!first)
		str << std::endl;
	      str << *l;
	      first = false;
	    }
	  std::string label = str.str();
	  if (!label.empty())
	    mol1->setProp("SRU_LABELS",label);
	}
    
      writer->write(*mol1);
      delete mol1;
      seen_inchi.insert(min_inchi);
    }
}


void GetFragmentBonds(RWMol *mol, std::vector<std::pair<unsigned int, unsigned int> > &bonds)
{
  for(ROMol::BondIterator bondIt=mol->beginBonds(); bondIt!=mol->endBonds();++bondIt)
    {
      unsigned int aid1 = (*bondIt)->getBeginAtomIdx();
      unsigned int aid2 = (*bondIt)->getEndAtomIdx();
      const Atom *atom1 = mol->getAtomWithIdx (aid1);
      const Atom *atom2 = mol->getAtomWithIdx (aid2);
	      
      std::vector<int> sgroups1;
      atom1->getPropIfPresent("_SGroup", sgroups1);
      std::vector<int> sgroups2;
      atom2->getPropIfPresent("_SGroup", sgroups2);
      std::set<int> unique_connected1;
      std::set<int> unique_connected2;
	      
      if (!sgroups2.empty())
	unique_connected2.insert(sgroups2.begin(), sgroups2.end());		 
      if (!sgroups1.empty())
	unique_connected1.insert(sgroups1.begin(), sgroups1.end());

      std::vector<int> intersection(std::min(unique_connected1.size(), unique_connected2.size()));
      std::vector<int>::iterator end = std::set_intersection (unique_connected1.begin(), unique_connected1.end(), unique_connected2.begin(), unique_connected2.end(), intersection.begin());
      for (std::vector<int>::iterator i = intersection.begin(); i != end; ++i)
	{
	  unique_connected1.erase(*i);
	  unique_connected2.erase(*i);
	}
      if (unique_connected1.empty() && unique_connected2.empty())
	continue;
      if (!unique_connected2.empty())
	{
	  std::vector<int> connected(unique_connected2.begin(), unique_connected2.end());
	  atom2->setProp("_IsSRU", connected);
	}
      if (!unique_connected1.empty())
	{
	  std::vector<int> connected(unique_connected1.begin(), unique_connected1.end());
	  atom1->setProp("_IsSRU", connected);
	}
	      
      std::vector<int> connected_before1;
      atom1->getPropIfPresent("_Connected", connected_before1);
      if (!connected_before1.empty())
	unique_connected2.insert(connected_before1.begin(), connected_before1.end());
	      
      std::vector<int> connected_before2;
      atom2->getPropIfPresent("_Connected", connected_before2);
      if (!connected_before2.empty())
	unique_connected1.insert(connected_before2.begin(), connected_before2.end());
	      
      if (!unique_connected2.empty())
	{
	  std::vector<int> connected(unique_connected2.begin(), unique_connected2.end());
	  atom1->setProp("_Connected", connected);
	}
      if (!unique_connected1.empty())
	{
	  std::vector<int> connected(unique_connected1.begin(), unique_connected1.end());
	  atom2->setProp("_Connected", connected);
	}
      bonds.push_back(std::make_pair(aid1, aid2));
    }
}

void CategorizeFragments(boost::shared_ptr<ROMol> m, EFragmentType &fragment_type, std::map<std::string, std::set<std::string> > &inchi_to_label,
			 const std::vector<std::string> &subscripts)
{
  std::string inchi;  
  RDKit::ExtraInchiReturnValues rv;
  try
    {
      inchi = MolTextToInchi(*m, rv, "/NPZz");
    } catch(...) {}
  
  std::set<int> unique_connected;
  bool is_sru = false;
  for(ROMol::AtomIterator atomIt=m->beginAtoms(); atomIt!=m->endAtoms(); ++atomIt)
    {
      std::vector<int> sgroups;
      if ((*atomIt)->getPropIfPresent("_IsSRU",sgroups))  
	{
	  is_sru = true;
	   
	  if (!inchi.empty())
	    for (size_t k = 0; k < sgroups.size(); k++)
	      if (sgroups[k] - 1 < subscripts.size())
		inchi_to_label[inchi].insert(subscripts[sgroups[k] - 1]);	
	}
      else
	{
	  std::vector<int> connected_before;
	  (*atomIt)->getPropIfPresent("_Connected", connected_before);
	  if (!connected_before.empty())
		unique_connected.insert(connected_before.begin(), connected_before.end());
	}
    }
  if (is_sru)
    {
      fragment_type = eSRU;
    }
  else if (unique_connected.size() == 1)
    {
      fragment_type = eEndpoint;
    }
  else if (unique_connected.size() > 1)
    {
      fragment_type = eConnection;
    }
}

void ClearSgroupsFromSplitFormerSRUs(boost::shared_ptr<ROMol> m, EFragmentType fragment_type)
{
  if (fragment_type == eSRU)
    return;
  
  for(ROMol::AtomIterator atomIt=m->beginAtoms(); atomIt!=m->endAtoms(); ++atomIt)
    if ((*atomIt)->hasProp("_SGroup"))
      {
	(*atomIt)->clearProp("_SGroup");
      }
}

std::vector<boost::shared_ptr<ROMol> > AddStarAtoms(std::vector<boost::shared_ptr<ROMol> > &separate_fragments, bool keep_wildcard)  
{
  std::vector<boost::shared_ptr<ROMol> > new_separate_fragments(separate_fragments);
  size_t num_fragments = separate_fragments.size();
  for (size_t i = 0; i < num_fragments; i++)   
    {
	if (separate_fragments[i]->getNumAtoms() == 1 && (*separate_fragments[i]->beginAtoms())->getAtomicNum() == 0 && !keep_wildcard)
	  continue;
	boost::shared_ptr<RWMol> new_mol(new RWMol(*separate_fragments[i]));
	RDKit::Conformer &conf = new_mol->getConformer();
	std::vector<RDGeom::Point3D> &coords = conf.getPositions();
	  
	std::map<int,int> bond_ids;
	for(ROMol::AtomIterator atomIt=separate_fragments[i]->beginAtoms(); atomIt!=separate_fragments[i]->endAtoms(); ++atomIt)
	  {
	    std::vector<int> ids;
	    if ((*atomIt)->getPropIfPresent("_bond_id", ids))
	      {
		for (auto id : ids)
		  bond_ids[id] = (*atomIt)->getIdx();
	      }
	  }
	
	for (size_t j = 0; j < num_fragments; j++)
	  if (i != j)
	    {
	      RDKit::Conformer &old_conf = separate_fragments[j]->getConformer();
	      std::vector<RDGeom::Point3D> &old_coords = old_conf.getPositions();

	      for(ROMol::AtomIterator atomIt=separate_fragments[j]->beginAtoms(); atomIt!=separate_fragments[j]->endAtoms(); ++atomIt)
		{
		  std::vector<int> ids;
		  if ((*atomIt)->getPropIfPresent("_bond_id", ids))
		    {
		      for (auto id : ids)
			if (bond_ids.find(id) != bond_ids.end())
			  {
			    int aid1 = bond_ids[id];
			    int old_aid2 = (*atomIt)->getIdx();
			    
			    QueryAtom *query=new QueryAtom(0);
			    query->setQuery(makeAtomNullQuery());
			    new_mol->addAtom(query);
			    int aid2 = new_mol->getLastAtom()->getIdx();
			    coords[aid2] = old_coords[old_aid2];
			    new_mol->addBond(aid1, aid2,RDKit::Bond::SINGLE);
			    std::vector<int> sgroups;
			    if (new_mol->getAtomWithIdx(aid1)->getPropIfPresent("_SGroup", sgroups))
			      new_mol->getBondBetweenAtoms (aid1, aid2)->setProp("_SGroup", sgroups);
			    
			    if (new_mol->getAtomWithIdx (aid1)->hasProp("_senior") || (*atomIt)->hasProp("_senior"))
			      {
				new_mol->setProp("FRAGMENT_TYPE", "head end group");
			      }
			    if (new_mol->getAtomWithIdx (aid1)->hasProp("_junior") || (*atomIt)->hasProp("_junior"))
			      {
				new_mol->setProp("FRAGMENT_TYPE", "tail end group");
			      }
			  }
		    }
		}
	    }
	new_separate_fragments[i] = boost::shared_ptr<ROMol>(new ROMol(*new_mol));
    }
  return(new_separate_fragments);
}

void ReconnectGraftSrus(std::vector<boost::shared_ptr<ROMol> > &separate_fragments, std::vector<EFragmentType> &fragment_types, bool keep_wildcard)  
{
  size_t num_fragments = separate_fragments.size();
  for (size_t i = 0; i < num_fragments; i++)
    if (fragment_types[i] == eSRU) 
    {
	if (separate_fragments[i]->getNumAtoms() == 1 && (*separate_fragments[i]->beginAtoms())->getAtomicNum() == 0 && !keep_wildcard)
	  continue;

	std::set<int> sss_set1;
	std::set<int> bond_ids;
	for(ROMol::AtomIterator atomIt=separate_fragments[i]->beginAtoms(); atomIt!=separate_fragments[i]->endAtoms(); ++atomIt)
	  {
	    std::vector<int> ids;
	    if ((*atomIt)->getPropIfPresent("_bond_id", ids))
	      {
		bond_ids.insert(ids.begin(), ids.end());
	      }
	     std::vector<int> sgroups;
	     if ((*atomIt)->getPropIfPresent("_SGroup", sgroups))
	       {
		 if (!sgroups.empty()) 
		   {
		     sss_set1.insert(sgroups.begin(), sgroups.end());
		   }
	       }
	  }
	
	std::set<int> sru;
	for (size_t j = 0; j < num_fragments; j++)
	  if (i < j && fragment_types[j] == eSRU)
	    {
	      std::set<int> sss_set2;
	      for(ROMol::AtomIterator atomIt=separate_fragments[j]->beginAtoms(); atomIt!=separate_fragments[j]->endAtoms(); ++atomIt)
		{
		  std::vector<int> sgroups;
		  if ((*atomIt)->getPropIfPresent("_SGroup", sgroups))
		    {
		      if (!sgroups.empty()) 
			{
			  sss_set2.insert(sgroups.begin(), sgroups.end());
			}
		    }
		}
	      std::vector<int> intersection(std::min(sss_set1.size(), sss_set2.size()));
	      std::vector<int>::iterator end = std::set_intersection (sss_set1.begin(), sss_set1.end(), sss_set2.begin(), sss_set2.end(), intersection.begin());
	      if (end != intersection.begin())
		for(ROMol::AtomIterator atomIt=separate_fragments[j]->beginAtoms(); atomIt!=separate_fragments[j]->endAtoms(); ++atomIt)
		  {
		    std::vector<int> ids;
		    if ((*atomIt)->getPropIfPresent("_bond_id", ids))
		      {
			for (auto id : ids)
			  if (bond_ids.find(id) != bond_ids.end())
			    {
			      sru.insert(j);
			    }
		      }
		  }
	    }
	
	RWMol *new_mol(new RWMol(*separate_fragments[i]));
	for (std::set<int>::const_iterator s = sru.begin(); s != sru.end(); ++s)
	  new_mol->insertMol(*separate_fragments[*s]);
	std::map<int, std::vector<int> > connection;
	for(ROMol::AtomIterator atomIt=new_mol->beginAtoms(); atomIt!=new_mol->endAtoms(); ++atomIt)
	  {
	    std::vector<int> ids;
	    if ((*atomIt)->getPropIfPresent("_bond_id", ids))
	      for (auto id : ids)
		if (bond_ids.find(id) != bond_ids.end())
		  {
		    connection[id].push_back((*atomIt)->getIdx());
		  }
	  }
	for (std::map<int, std::vector<int> >::const_iterator c = connection.begin(); c != connection.end(); ++c)
	  if (c->second.size() == 2)
	    {
	      int aid1 = c->second.front();
	      int aid2 = c->second.back();
	      new_mol->addBond(aid1,aid2,Bond::SINGLE);
	      new_mol->getAtomWithIdx (aid1)->clearProp("_bond_id");
	      new_mol->getAtomWithIdx (aid2)->clearProp("_bond_id");
	    }


	separate_fragments.push_back(boost::shared_ptr<ROMol>(new ROMol(*new_mol)));
	fragment_types.push_back(eConnection);

	delete new_mol;
    }
}

void AddHydrogensToCanonicalMap(boost::shared_ptr<ROMol> mol, std::map<int,int> &canonical)
{
  int max_mapped = -1;
  for (std::map<int,int>::const_iterator i = canonical.begin(); i != canonical.end(); ++i)
    {
      if (i->second > max_mapped)
	max_mapped = i->second;
    }
  std::map<int,std::vector<int> > new_canonical;
  for(ROMol::AtomIterator atomIt=mol->beginAtoms(); atomIt!=mol->endAtoms();++atomIt)
    {
      int aid = (*atomIt)->getIdx();
      if (canonical.find(aid) == canonical.end())
	{
	  int min_neighbor = INT_MAX;
	  ROMol::ADJ_ITER begin,end;
	  boost::tie(begin,end) = mol->getAtomNeighbors(*atomIt);
	  while(begin!=end)
	    {
	      if (canonical.find(*begin) != canonical.end() && canonical[*begin] < min_neighbor)
		{
		  min_neighbor = canonical[*begin];
		}
	      ++begin;
	    }
	  new_canonical[min_neighbor].push_back(aid);		  
	}
    }
  for (std::map<int,std::vector<int> >::const_iterator i = new_canonical.begin(); i != new_canonical.end(); ++i)
    {
      for (std::vector<int>::const_iterator j = i->second.begin(); j != i->second.end(); ++j)
	{
	  max_mapped++;
	  canonical[*j] = max_mapped;
	}
    }
}

void AddSalts(boost::shared_ptr<RWMol> mol, const std::vector<boost::shared_ptr<ROMol> > &salts, const std::set<int> &sss_set)
{
  for (auto s : salts)
    {
      int sss = -1;
      if (s->getPropIfPresent("_sru_salt", sss) && sss_set.find(sss) != sss_set.end())
	{
	  mol->insertMol(*s);
	}
    }
}

void PrepareOutput(std::vector<boost::shared_ptr<ROMol> > &separate_fragments,
		   std::vector<boost::shared_ptr<ROMol> > &separate_fragments_with_stars,
		   const std::vector<EFragmentType> &fragment_types,
		   const std::map<std::string, std::set<std::string> > &inchi_to_label,
		   bool is_chiral_flag, int chirality_flag, bool keep_wildcard, int polymer_label,
		   const std::vector<boost::shared_ptr<ROMol> > &salts)
{ 
  std::vector<boost::shared_ptr<ROMol> > fragments;
  std::set<std::string> seen_inchi;
  size_t num_fragments = separate_fragments.size();
  for (size_t i = 0; i < num_fragments; i++)
    {
      boost::shared_ptr<ROMol> m = separate_fragments[i];
      boost::shared_ptr<ROMol> m_star = separate_fragments_with_stars[i];

      if (m->getNumAtoms() == 1 && (*m->beginAtoms())->getAtomicNum() == 0 && !keep_wildcard)
	continue;
	  
      std::string inchi;
      std::string aux;
      RDKit::ExtraInchiReturnValues rv;
      try
	{
	  inchi = MolTextToInchi(*m, rv, "/NPZz");
	  aux = rv.auxInfoPtr;
	} catch(...) {}     
         
      
      std::map<std::string, std::set<std::string> >::const_iterator labels = inchi_to_label.find(inchi);
      if (labels != inchi_to_label.end())
	{
	  std::stringstream str;
	  bool first = true;
	  for (std::set<std::string>::const_iterator l = labels->second.begin(); l != labels->second.end(); ++l)
	    {
	      if (!first)
		str << std::endl;
	      str << *l;
	      first = false;
	    }
	  std::string label = str.str();
	  if (!label.empty())
	    m_star->setProp("SRU_LABELS",label);
	}

      if (fragment_types[i] == eSRU)
	{
	  
	  std::set<int> sss_set;
	  FindAtomSGroups(m.get(), sss_set);
	  KeepSGroup(m_star.get(), sss_set);
	  
	  boost::shared_ptr<RWMol> mol(new RWMol(*m_star));

	  RDKit::Conformer &conf =mol->getConformer();
	  std::vector<RDGeom::Point3D> &coords = conf.getPositions();

	  MoveBrackets(mol.get(), coords, false);
	  AddSalts(mol, salts, sss_set);
	  m_star = mol;
	  //inchi = MolTextToInchi(*m_star, rv, "/Polymers");
	  //aux = rv.auxInfoPtr;
	  inchi.clear();
	  aux.clear();
	}
      
      std::string i_inchi = MolTextToInchi(*m_star, rv, "/NPZz"); 
      aux = rv.auxInfoPtr;

      if (seen_inchi.find(i_inchi) != seen_inchi.end())
	continue;
      if (!i_inchi.empty())
	seen_inchi.insert(i_inchi);
      
      std::map<int,int> canonical = input_to_canonical(i_inchi,  aux);
      AddHydrogensToCanonicalMap(m_star, canonical);

      std::vector<int> connecting_atoms;
      if (fragment_types[i] == eSRU)
	{
	  std::vector<std::string> connecting_atoms_label = GetConnectingAtomsSRU(m_star.get(), canonical, connecting_atoms);
	  if (connecting_atoms_label.size() == 2)
	    {
	      m_star->setProp("CONNECTING_ATOMS_HEAD",connecting_atoms_label.front());
	      m_star->setProp("CONNECTING_ATOMS_TAIL",connecting_atoms_label.back());
	    }
	}
      else
	{
	  std::string connecting_atoms_label = GetConnectingAtoms(m_star.get(), canonical, connecting_atoms);
	  if (!connecting_atoms_label.empty())
	    m_star->setProp("CONNECTING_ATOMS",connecting_atoms_label);
	}
      
      switch (fragment_types[i])
	{
	case eDisconnected :  m_star->setProp("FRAGMENT_TYPE", "disconnected"); break;
	case eEndpoint : if (!m_star->hasProp("FRAGMENT_TYPE"))
	    {
	      m_star->setProp("FRAGMENT_TYPE", "endpoint");
	    }
	  break;
	case eSRU :  if (connecting_atoms.size() == 2)
	    m_star->setProp("FRAGMENT_TYPE", "linear sru");
	  else if (connecting_atoms.size() == 3)
	    m_star->setProp("FRAGMENT_TYPE", "branched sru");
	  else if (connecting_atoms.size() == 4)
	    m_star->setProp("FRAGMENT_TYPE", "ladder sru");
	  else
	    m_star->setProp("FRAGMENT_TYPE", "non-linear sru");
	  break;
	case eConnection :  m_star->setProp("FRAGMENT_TYPE", "connection"); break;
	default : m_star->setProp("FRAGMENT_TYPE", "unknown"); break;
	}           
            
      if (!i_inchi.empty())
	{
	  m_star->setProp("Computed_B_InChI", i_inchi);
	  std::string inchikey = RDKit::InchiToInchiKey(i_inchi);
	  m_star->setProp("Computed_B_InChIKey", inchikey);
	}
      
      m_star->setProp("POLYMER_LABEL", polymer_label);
      RemoveAllSGroups(m_star.get());

      if (!canonical.empty())
	{
	  std::vector<unsigned int> new_order(canonical.size());
	  for (auto &c : canonical)
	    new_order[c.second] = c.first;
	  MolOps::findSSSR(*m_star);
	  ROMol *reordered_mol = MolOps::renumberAtoms(*m_star, new_order);
	  Conformer &old_conf = m_star->getConformer();
	  Conformer &conf = reordered_mol->getConformer();
	  conf.set3D(old_conf.is3D());
	  std::vector<RDGeom::Point3D>& coords = conf.getPositions();
	  std::vector<RDGeom::Point3D> &old_coords = old_conf.getPositions();
	  for (size_t ii = 0; ii < coords.size(); ++ii)
	    coords[ii] = old_coords[new_order[ii]];
	  auto prop_list = m_star->getPropList(false, false);
	  for (const std::string &prop : prop_list)
	    {
	      std::string val;
	      m_star->getProp(prop, val);
	      reordered_mol->setProp(prop, val);
	    }
	  m_star = boost::shared_ptr<ROMol>(reordered_mol);
	}
       
      fragments.push_back(m_star);
    }
  std::swap(fragments, separate_fragments);
}

void SplitFragments(RWMol *orig_mol, std::vector<boost::shared_ptr<ROMol> > &separate_fragments,
		    bool is_chiral_flag, int chirality_flag, bool keep_wildcard, int polymer_label,
		    const std::vector<boost::shared_ptr<ROMol> > &salts)
{
  RWMol *mol = new RWMol(*orig_mol);  
	    
  std::vector<std::string> subscripts;
  mol->getPropIfPresent("_SGroupSubscript",subscripts);  
  
  std::vector<std::pair<unsigned int, unsigned int> > bonds;
  GetFragmentBonds(mol, bonds);
  int bond_id = 0;
  for (size_t i = 0; i < bonds.size(); i++)
    {
      unsigned int aid1 = bonds[i].first;
      unsigned int aid2 = bonds[i].second;
      std::vector<int> bond_ids1, bond_ids2;
      mol->getAtomWithIdx(aid1)->getPropIfPresent("_bond_id", bond_ids1);
      bond_ids1.push_back(bond_id);
      mol->getAtomWithIdx(aid2)->getPropIfPresent("_bond_id", bond_ids2);
      bond_ids2.push_back(bond_id);
      mol->getAtomWithIdx(aid1)->setProp("_bond_id", bond_ids1);
      mol->getAtomWithIdx(aid2)->setProp("_bond_id", bond_ids2);
      mol->removeBond(aid1, aid2);
      bond_id++;
    }

  separate_fragments = MolOps::getMolFrags(*mol,false,NULL);
  size_t num_fragments = separate_fragments.size();
  std::map<std::string, std::set<std::string> > inchi_to_label;
  std::vector<EFragmentType> fragment_types(num_fragments, eDisconnected);
  for (size_t i = 0; i < num_fragments; i++)
    {
      CategorizeFragments(separate_fragments[i], fragment_types[i], inchi_to_label, subscripts);
      ClearSgroupsFromSplitFormerSRUs(separate_fragments[i], fragment_types[i]);
    }

  //ReconnectGraftSrus(separate_fragments, fragment_types, keep_wildcard);
  std::vector<boost::shared_ptr<ROMol> > separate_fragments_with_stars = AddStarAtoms(separate_fragments, keep_wildcard);
  PrepareOutput(separate_fragments, separate_fragments_with_stars, fragment_types, inchi_to_label, is_chiral_flag, chirality_flag, keep_wildcard, polymer_label, salts);
}

void SaveBrackets(ROMol *mol, std::vector<std::vector<double> >  &sgroups)
{
  if (mol->hasProp("_SGroupBrackets")) 
    {      
      mol->getProp("_SGroupBrackets",sgroups);
    }
}

void RestoreBrackets(ROMol *mol, const std::vector<std::vector<double> >  &sgroups, const std::set<int> &sss_set) // TODO fix for graft polymers
{
  std::vector<std::vector<double> >  new_sgroups;
   if (mol->hasProp("_SGroupBrackets")) 
    {      
      mol->getProp("_SGroupBrackets",new_sgroups);
    }
   for (size_t i = 0; i < new_sgroups.size(); i++)
     {
       if (sss_set.find(i + 1) != sss_set.end())
	 {
	   new_sgroups[i] = sgroups[i];
	 }
     }
   if (!new_sgroups.empty())
     mol->setProp("_SGroupBrackets",new_sgroups);
}

void FindAtomSGroups(ROMol *mol, std::set<int> &sss_set)
{
  for(ROMol::AtomIterator atomIt=mol->beginAtoms(); atomIt!=mol->endAtoms(); ++atomIt)
    {
      std::vector<int> sgroups;
      if ((*atomIt)->getPropIfPresent("_SGroup", sgroups))
	{
	  if (!sgroups.empty()) 
	    {
	      sss_set.insert(sgroups.begin(), sgroups.end());
	    }
	}
    }

}

void RemoveAllSGroups(ROMol *m)
{
  if ( m->hasProp("_SGroupBrackets") )
    m->clearProp("_SGroupBrackets");
  if ( m->hasProp("_SGroupTypes") )
    m->clearProp("_SGroupTypes");
  if ( m->hasProp("_SGroupSubtypes") )
    m->clearProp("_SGroupSubtypes");
  if ( m->hasProp("_SGroupLabels") )
    m->clearProp("_SGroupLabels");
  if ( m->hasProp("_SGroupConn") )
    m->clearProp("_SGroupConn");
  if ( m->hasProp("_SGroup") )
    m->clearProp("_SGroup");
  if ( m->hasProp("_SGroupSubscript") )
    m->clearProp("_SGroupSubscript");
  for(ROMol::AtomIterator atomIt=m->beginAtoms(); atomIt!=m->endAtoms();++atomIt)
    {
      if ( (*atomIt)->hasProp("_SGroup") )
	(*atomIt)->clearProp("_SGroup");
      if ( (*atomIt)->hasProp("_IsSRU") )
	(*atomIt)->clearProp("_IsSRU");
      if ( (*atomIt)->hasProp("_Connected") )
	(*atomIt)->clearProp("_Connected");
      if ( (*atomIt)->hasProp("_bond_id") )
	{
	  (*atomIt)->clearProp("_bond_id");
	}
      if ( (*atomIt)->hasProp("_branched") )
	(*atomIt)->clearProp("_branched");
    }
      
  for(ROMol::BondIterator bondIt=m->beginBonds(); bondIt!=m->endBonds();++bondIt)
    if ( (*bondIt)->hasProp("_SGroup") )
      (*bondIt)->clearProp("_SGroup");
}

int GetNeighborStar(ROMol *mol, int aid)
{
  const Atom *atom = mol->getAtomWithIdx(aid);
  ROMol::ADJ_ITER begin, end;
  boost::tie(begin,end) = mol->getAtomNeighbors(atom);
  while(begin!=end)
    {
      if (mol->getAtomWithIdx(*begin)->getAtomicNum() == 0)
	{
	  aid = *begin;
	  break;
	}
      ++begin;
    }
  return aid;
}

std::string GetConnectingAtoms(ROMol *m, std::map<int,int> &canonical, std::vector<int> &aids)
{
  aids.clear();
  bool first = true;
  std::stringstream connecting_atoms;
  for(ROMol::AtomIterator atomIt=m->beginAtoms(); atomIt!=m->endAtoms();++atomIt)
    {
      if ( (*atomIt)->hasProp("_bond_id") )
        {
          int aid = (*atomIt)->getIdx();
          aids.push_back(aid);
          if (!first)
            connecting_atoms << std::endl;
          connecting_atoms << canonical[GetNeighborStar(m, aid)] + 1;
          first = false;
        }
    }
  std::string out = connecting_atoms.str();
  if (aids.size() == 2 && m->getAtomWithIdx(aids.front())->hasProp("_junior") && m->getAtomWithIdx(aids.back())->hasProp("_senior"))
    {
      std::stringstream connecting_atoms2;
      connecting_atoms2 << canonical[GetNeighborStar(m, aids.back())] + 1;
      connecting_atoms2 << std::endl;
      connecting_atoms2 << canonical[GetNeighborStar(m, aids.front())] + 1;
      out = connecting_atoms2.str();
    }
  return out;
}

std::vector<std::string> GetConnectingAtomsSRU(ROMol *m, std::map<int,int> &canonical, std::vector<int> &aids)
{
  aids.clear();
  int branched = -1;
  for(ROMol::AtomIterator atomIt=m->beginAtoms(); atomIt!=m->endAtoms();++atomIt)
    {
      if ( (*atomIt)->hasProp("_bond_id"))
	{
	  int aid = (*atomIt)->getIdx();
	  aids.push_back(aid);
	}
      if ( (*atomIt)->hasProp("_branched"))
	branched = (*atomIt)->getIdx();
    }
  
  std::vector<std::string> out;
  std::stringstream connecting_atoms1, connecting_atoms2;
  if (aids.size() == 1)
    {
      connecting_atoms1 << canonical[GetNeighborStar(m, aids.front())] + 1;
      out.push_back(connecting_atoms1.str());
    }
  else if (aids.size() == 2)
    {
      if (m->getAtomWithIdx(aids.front())->hasProp("_junior") && m->getAtomWithIdx(aids.back())->hasProp("_senior"))
	{
	  connecting_atoms1 << canonical[GetNeighborStar(m, aids.back())] + 1;
	  connecting_atoms2 << canonical[GetNeighborStar(m, aids.front())] + 1;
	}
      else
	{
	  connecting_atoms1 << canonical[GetNeighborStar(m, aids.front())] + 1;
	  connecting_atoms2 << canonical[GetNeighborStar(m, aids.back())] + 1;
	}
      if (branched >= 0)
	{
	  connecting_atoms2 << std::endl;
	  connecting_atoms2 << canonical[branched] + 1;
	  aids.push_back(branched);
	}
      out.push_back(connecting_atoms1.str());
      out.push_back(connecting_atoms2.str());
    }
  else if (aids.size() > 2)
    {
      RDKit::Conformer &conf =m->getConformer();
      std::vector<RDGeom::Point3D> &coords = conf.getPositions();
      double middle = 0;
      for (auto a : aids)
	{
	  middle += coords[a].x;
	}
      middle /= aids.size();
      bool found1(false), found2(false);
      for (auto a : aids)
	{
	  if (coords[a].x <= middle)
	    {
	      if (found1)
		connecting_atoms1 << std::endl;	      
	      connecting_atoms1 << canonical[GetNeighborStar(m, a)] + 1;
	      found1 = true;
	    }
	  else
	    {
	      if (found2)
		connecting_atoms2 << std::endl;	 
	      connecting_atoms2 << canonical[GetNeighborStar(m, a)] + 1;
	      found2 = true;
	    }
	}
      if (found1 && found2)
	{
	  out.push_back(connecting_atoms1.str());
	  out.push_back(connecting_atoms2.str());
	}
    }
  return out;
}

template<class T>
void FilterSGroup(ROMol *mol, const std::set<int> &sss_set, const std::string &prop)
{
  std::vector<T> old, new_vec;
  if ( mol->getPropIfPresent(prop, old) )
    {
      for (size_t i = 0; i < old.size(); i++)
	{
	  if (sss_set.find(i + 1)  != sss_set.end())
	    new_vec.push_back(old[i]);
	}
      if (!new_vec.empty())
	 mol->setProp(prop, new_vec);
       else
	 mol->clearProp(prop);
    }
}

void KeepSGroup(ROMol *mol, const std::set<int> &sss_set)
{
  FilterSGroup<std::vector<double> >(mol, sss_set, "_SGroupBrackets");
  FilterSGroup<int>(mol, sss_set, "_SGroupTypes");
  FilterSGroup<int>(mol, sss_set, "_SGroupSubtypes");
  FilterSGroup<std::string>(mol, sss_set, "_SGroupLabels");
  FilterSGroup<int>(mol, sss_set, "_SGroupConn");
  FilterSGroup<std::string>(mol, sss_set, "_SGroupSubscript");
  if (!sss_set.empty() && *sss_set.begin() > 1)
    {
      int offset = *sss_set.begin() - 1;
      std::vector<std::string> labels;
      if (mol->getPropIfPresent("_SGroupLabels", labels))
	{
	  for (size_t i = 0; i < labels.size(); i++)
	    {
	      int nl = atoi(labels[i].c_str()) - offset;
	      std::stringstream str;
	      str << nl;
	      labels[i] = str.str();
	    }
	  mol->setProp("_SGroupLabels", labels);
	}
      
      for(ROMol::AtomIterator atomIt=mol->beginAtoms(); atomIt!=mol->endAtoms();++atomIt)
	{
	  std::vector<int> sgroups;
	  if ( (*atomIt)->getPropIfPresent("_SGroup", sgroups) )
	    {
	      for (size_t i = 0; i < sgroups.size(); i++)
		sgroups[i] -= offset;	      
	      (*atomIt)->setProp("_SGroup", sgroups);
	    }
	}

      for(ROMol::BondIterator bondIt=mol->beginBonds(); bondIt!=mol->endBonds();++bondIt)
	{
	  std::vector<int> sgroups;
	  if ( (*bondIt)->getPropIfPresent("_SGroup", sgroups) )
	    {
	      for (size_t i = 0; i < sgroups.size(); i++)
		sgroups[i] -= offset;	      
	      (*bondIt)->setProp("_SGroup", sgroups);
	    }
	}
    }
}
