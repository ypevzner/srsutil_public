/*
written by Igor Filippov, VIF Innovations, LLC, 2013

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
"AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

#include "seq2mol.h"

using namespace std;

pair<int,int> get_attachment_points_na(RDKit::ROMol* m)
{
  int first = -1;
  int last = -1;
  for(RDKit::ROMol::AtomIterator a=m->beginAtoms(); a!=m->endAtoms();++a)
    if ((*a)->getSymbol() == "*")
      {
	int num = (*a)->getIsotope();
	if (num == 3) first = (*a)->getIdx();
	if (num == 4 && last == -1) last = (*a)->getIdx();
	if (num == 5) last = (*a)->getIdx();
      }
  if (first == -1) first = 0;
  if (last == -1) last = m->getNumAtoms()-1;
  return make_pair(first,last);
}

void flip_sugar_up(RDKit::ROMol *mol)
{
  if (mol->hasProp("RESIDUES"))
    {
      string prop;
      mol->getProp("RESIDUES",prop);
      if (prop == "SUGAR")
	{
	  int conn = -1;
	  for(RDKit::ROMol::AtomIterator a=mol->beginAtoms(); a!=mol->endAtoms();++a)
	    if ((*a)->getSymbol() == "*" && (*a)->getIsotope() == 4)
	      conn = (*a)->getIdx();
	  if (conn >= 0 && mol->getConformer().getPositions()[conn].y < mol->getConformer().getPositions().back().y)
	    {
	      RDGeom::Point3D v = RDGeom::Point3D(0,-1,0);
	      double angle = v.signedAngleTo(RDGeom::Point3D(0,1,0));
	      RDGeom::Transform3D t1;
	      t1.SetRotation(angle, RDGeom::X_Axis);
	      MolTransforms::transformMolsAtoms(mol,t1);
	    }
	}
    }
}

RDKit::ROMol* rearrange_residue(RDKit::ROMol* nmol)
{
  pair<int,int> points = get_attachment_points_na(nmol);
  string residues,type;
  if (nmol->hasProp("RESIDUES"))
    nmol->getProp("RESIDUES",residues);
  if (nmol->hasProp("TYPE"))
    nmol->getProp("TYPE",type);
  RDKit::RWMol* mol = reset_moltab(nmol,points.first,points.second);
  if (!residues.empty())
    mol->setProp("RESIDUES",residues);
  if (!type.empty())
    mol->setProp("TYPE",type);
  add_coordinates_single(mol);
  flip_sugar_up(mol);
  return mol;
}

string mol_to_sdf_na(RDKit::RWMol *mol)
{
  unsigned int chirality_flag = 1;
  mol->setProp("_MolFileChiralFlag",chirality_flag);
  try {
    RDKit::MolOps::sanitizeMol(*mol);
  } catch(...) {}
  string molBlock=RDKit::MolToMolBlock(*(static_cast<RDKit::ROMol *>(mol)));
  delete mol;
  return molBlock;
}

void create_map_na(map<char,RDKit::ROMol*> &m)
{
  m['C'] = RDKit::SmilesToMol("N1C=CC(=NC1=O)N");
  m['G'] = RDKit::SmilesToMol("N1C2=C(N=C1)C(=O)N=C(N2)N");
  m['A'] = RDKit::SmilesToMol("N1C2=C(N=C1)C(=NC=N2)N");
  m['T'] = RDKit::SmilesToMol("N1C=C(C(=O)NC1=O)C");
  m['U'] = RDKit::SmilesToMol("N1C=C(C(=O)NC1=O)");
  
  add_coordinates(m);
}

void prepare_residues(vector<RDKit::ROMol*>  &sugars,vector<RDKit::ROMol*>  &linkers, const vector<RDKit::ROMol*> &modified_mols, const int num_seq, const int length,
		      RDKit::ROMol* std_sugar, RDKit::ROMol* std_linkage, RDKit::ROMol* std_sugar_first)
{
  sugars.resize(length,NULL);
  linkers.resize(length,NULL);
  for (int i=0; i<modified_mols.size(); i++)
    {
      RDKit::ROMol* mol = modified_mols[i];
      if (mol->hasProp("RESIDUES"))
	{
	  string line;
	  mol->getProp("RESIDUES",line);
	  vector<string> seq_pos = split(line,'-');
	  if (seq_pos.size() == 1)
	    seq_pos.push_back(seq_pos.front());
	  if (seq_pos.size() != 2)
	    {
	      cerr << "Misformed residue position line entry " << line << endl;
	      return;
	    }
	  vector<string> first = split(seq_pos[0],'_');
	  if (first.size() != 2)
	    {
	      cerr << "Misformed residue position line entry " << line << endl;
	      return;
	    }
	  vector<string> second = split(seq_pos[1],'_');
	  if (second.size() != 2)
	    {
	      cerr << "Misformed residue position line entry " << line << endl;
	      return;
	    }
	  int seq1 = atoi(first[0].c_str())-1;
	  int pos1 = atoi(first[1].c_str())-1;
	  int seq2 = atoi(second[0].c_str())-1;
	  int pos2 = atoi(second[1].c_str())-1;
	  if (seq1 < 0 || pos1 < 0 || seq2 < 0 || pos2 < 0)
	    cerr << "Residue position did not start at 1 " << line << endl;
	  if (num_seq >= seq1 && num_seq <= seq2)
	   if (mol->hasProp("TYPE"))
	     {
	       string type;
	       mol->getProp("TYPE",type);
	       if (type == "SUGAR")
		 {
		   for (int j=0; j<length; j++)
		     if (j >= pos1 && j <= pos2)
		       {
			 sugars[j] = mol;
		       }
		 }
	       else if (type == "LINKAGE")
		 {
		   for (int j=0; j<length; j++)
		     if (j >= pos1 && j <= pos2)
		       {
			 linkers[j] = mol;
		       }
		 }
	       else
		 {
		   cerr << "Unknown type: " << type << endl;
		   return;
		 }
	     }
	}
    }
  for (int j=0; j<length; j++)
    if (sugars[j] == NULL)
      {
	if (j == 0)
	  sugars[j] = std_sugar_first;
	else
	  sugars[j] = std_sugar;
      }
  for (int j=0; j<length; j++)
    if (linkers[j] == NULL)
      linkers[j] = std_linkage;
}

void copy_coordinates_na(RDKit::RWMol* mol, RDKit::ROMol* mol1, RDGeom::Point3D& offset, int oldsize, int beginning, int ending, int base)
{
  vector<RDGeom::Point3D>& coords = mol1->getConformer().getPositions();
  mol->getConformer().resize(mol->getNumAtoms());
  vector<RDGeom::Point3D>& new_coords = mol->getConformer().getPositions();
  int i = oldsize;
  for (int j = 0; i < new_coords.size() && j < coords.size(); j++)
    if (j != beginning && j != ending && j != base )
      new_coords[i++] = coords[j]+offset;
}

RDKit::RWMol* process_na(const string &line,const map<char,RDKit::ROMol*> &a2smi,vector<RDKit::ROMol*>  &sugars,vector<RDKit::ROMol*>  &linkers, bool v3000)
{
  RDKit::RWMol* mol = new RDKit::RWMol();
  RDKit::Conformer *conf = new RDKit::Conformer();
  conf->set3D(false);
  mol->addConformer(conf);
  
  RDGeom::Point3D offset(0,0,0);
  int prior = 0;
  for (int i=0; i<line.length(); i++)
    {
      char nucleoside = line[i];
      int beginning = 0;
      int ending = 0;
      int base = -1;
      int connect = -1;
      map<int,int> old_to_new;
       RDKit::ROMol* mol1 = sugars[i];
      for(RDKit::ROMol::AtomIterator a=mol1->beginAtoms(); a!=mol1->endAtoms();++a)
	{
	  if (i > 0 && (*a)->getIdx() == 0) continue;
	  if ((*a)->getIdx() == mol1->getNumAtoms()-1) continue;
	  if ((*a)->getSymbol() == "*" && (*a)->getIsotope() == 4) 
	    {
	      base = (*a)->getIdx();
	      continue;
	    }
	  mol->addAtom((*a)->copy());
	  old_to_new[(*a)->getIdx()] = mol->getLastAtom()->getIdx();
	}
      copy_coordinates_na(mol,mol1,offset,mol->getNumAtoms() - old_to_new.size(), (i == 0 ? -1 : 0), mol1->getNumAtoms()-1,base);
      for(RDKit::ROMol::BondIterator b=mol1->beginBonds(); b!=mol1->endBonds();++b)
	{
	  if ( i > 0)
	    {
	      if ((*b)->getBeginAtomIdx() == 0)
		{
		  beginning = (*b)->getEndAtomIdx();
		  continue;
		}
	      if ((*b)->getEndAtomIdx() == 0)
		{
		  beginning = (*b)->getBeginAtomIdx();
		  continue;
		}
	    }
	  if ((*b)->getBeginAtomIdx() == mol1->getNumAtoms()-1)
	    {
	      ending = (*b)->getEndAtomIdx();
	      continue;
	    }
	  if ( (*b)->getEndAtomIdx() == mol1->getNumAtoms()-1)
	    {
	      ending = (*b)->getBeginAtomIdx();
	      continue;
	    }
	  if ((*b)->getBeginAtomIdx() == base)
	    {
	      connect = (*b)->getEndAtomIdx();
	      continue;
	    }
	  if ( (*b)->getEndAtomIdx() == base)
	    {
	      connect = (*b)->getBeginAtomIdx();
	      continue;
	    }
	  mol->addBond(old_to_new[(*b)->getBeginAtomIdx()],old_to_new[(*b)->getEndAtomIdx()],(*b)->getBondType());
	}
      if (i > 0)
	mol->addBond(prior,old_to_new[beginning],RDKit::Bond::SINGLE);
      prior = old_to_new[ending];
      offset += mol1->getConformer().getPositions().back();

      
      if (connect >= 0) // add base
	{
	  int new_connect = old_to_new[connect];
	  map<char,RDKit::ROMol*>::const_iterator smi = a2smi.find(nucleoside);
	  if ( smi == a2smi.end())
	    {
	      cerr << "Invalid character in sequence string: "<<nucleoside<<" in "<<line<<endl;
	      return NULL;
	    }
	  RDKit::ROMol* mol3 = smi->second;
	  old_to_new.clear();
	  for(RDKit::ROMol::AtomIterator a=mol3->beginAtoms(); a!=mol3->endAtoms();++a)
	    {
	      mol->addAtom((*a)->copy());
	      old_to_new[(*a)->getIdx()] = mol->getLastAtom()->getIdx();
	    }

	  RDGeom::Point3D base_offset = mol->getConformer().getPositions()[new_connect];
	  base_offset += RDGeom::Point3D(0,10,0); 
	  vector<RDGeom::Point3D>& coords = mol3->getConformer().getPositions();
	  int length = coords.size();
	  mol->getConformer().resize(mol->getNumAtoms());
	  vector<RDGeom::Point3D>& new_coords = mol->getConformer().getPositions();
	  int n = new_coords.size();
	  for (int i = 0; i < length; i++)
	    new_coords[n-length+i] = coords[i]+base_offset;

	  for(RDKit::ROMol::BondIterator b=mol3->beginBonds(); b!=mol3->endBonds();++b)
	    mol->addBond(old_to_new[(*b)->getBeginAtomIdx()],old_to_new[(*b)->getEndAtomIdx()],(*b)->getBondType());
	    
	  mol->addBond(new_connect,old_to_new[0],RDKit::Bond::SINGLE);
	  //mol->getAtomWithIdx(new_connect)->setChiralTag(RDKit::Atom::CHI_UNSPECIFIED);
	}

      if (i != line.length() - 1) // add linker
	{      
	  RDKit::ROMol* mol2 = linkers[i];
	  beginning = 0;
	  ending = 0;
	  old_to_new.clear();
	  for(RDKit::ROMol::AtomIterator a=mol2->beginAtoms(); a!=mol2->endAtoms();++a)
	    {
	      if ((*a)->getIdx() == 0) continue;
	      if ((*a)->getIdx() == mol2->getNumAtoms()-1) continue;
	      mol->addAtom((*a)->copy());
	      old_to_new[(*a)->getIdx()] = mol->getLastAtom()->getIdx();
	    }
	  copy_coordinates_na(mol,mol2,offset,mol->getNumAtoms() - old_to_new.size(), 0, mol2->getNumAtoms()-1,-1);
	  for(RDKit::ROMol::BondIterator b=mol2->beginBonds(); b!=mol2->endBonds();++b)
	    {
	      if ((*b)->getBeginAtomIdx() == 0)
		{
		  beginning = (*b)->getEndAtomIdx();
		  continue;
		}
	      if ((*b)->getEndAtomIdx() == 0)
		{
		  beginning = (*b)->getBeginAtomIdx();
		  continue;
		}
	      if ((*b)->getBeginAtomIdx() == mol2->getNumAtoms()-1)
		{
		  ending = (*b)->getEndAtomIdx();
		  continue;
		}
	      if ( (*b)->getEndAtomIdx() == mol2->getNumAtoms()-1)
		{
		  ending = (*b)->getBeginAtomIdx();
		  continue;
		}
	      mol->addBond(old_to_new[(*b)->getBeginAtomIdx()],old_to_new[(*b)->getEndAtomIdx()],(*b)->getBondType());
	    }
	  mol->addBond(prior,old_to_new[beginning],RDKit::Bond::SINGLE);
	  prior = old_to_new[ending];
	  offset += mol2->getConformer().getPositions().back();
	}
      else
	{
	  mol->addAtom(new RDKit::Atom(1));
	  mol->getConformer().resize(mol->getNumAtoms());
	  mol->getConformer().getPositions().back() = offset;
	  mol->addBond(prior,mol->getLastAtom()->getIdx(),RDKit::Bond::SINGLE);
	}

      if (!v3000)
	{
	  if (mol->getNumAtoms() > 999)
	    {
	      cerr << "Maximum number of atoms for mol block exceeded in " << line << endl;
	      return NULL;
	    }
	  if (mol->getNumBonds() > 999)
	    {
	      cerr << "Maximum number of bonds for mol block exceeded in " << line << endl;
	      return NULL;
	    }
	}
    }
  return mol;
}

void delete_residues(vector<RDKit::ROMol*>  &res)
{
  for (int i=0; i<res.size(); i++)
    delete res[i];
}
