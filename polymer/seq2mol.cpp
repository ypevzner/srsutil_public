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

RDKit::RWMol* process_seq(const string& seq, const map<char,RDKit::ROMol*>& a2smi, int num, map< pair<int,int>, pair<int,int> >& sp, map< pair<int,int>,int >& sp_atoms,
			  map<pair<int,int>,pair<RDKit::ROMol*,bool> >& modified_mols, bool v3000)
{
  RDKit::RWMol *mol=new RDKit::RWMol();
  RDKit::Conformer *conf = new RDKit::Conformer();
  conf->set3D(false);
  mol->addConformer(conf);

  int n_atom = 0;
  int prior = -1;
  RDGeom::Point3D offset(0,0,0);
  int m=0;
  for (int mm=0; mm<seq.size(); mm++)
    {
      char a = seq[mm];
      if (a == ' ') 
	continue;

      if (sp.find(make_pair(num,m)) != sp.end())
	{
	  if (toupper(a) != 'C')
	    {
	      cerr << "Not a cysteine at disulphide bond location" <<endl;
	      exit(3);
	    }
	  sp_atoms[make_pair(num,m)] = mol->getNumAtoms() + SULPHUR_POSITION;
	}
      bool lastmol = (mm == seq.size()-1);
      RDKit::ROMol* mol1;
      bool delete_mol1 = false;
      map<pair<int,int>,pair<RDKit::ROMol*,bool> >::iterator mod = modified_mols.find(make_pair(num,m));
      if ( mod != modified_mols.end() ) 
	{
	  mol1 = mod->second.first;
	  if (mod->second.second && m == 0 && seq.size() > 1) // rotating if first fragment and only one attachment point
	    {
	      RDKit::RWMol* mol2(new RDKit::RWMol(*mol1,true));
	      RDKit::RWMol* mol3 = reset_moltab(mol2,(mol2->getLastAtom())->getIdx(),0);
	      add_coordinates_single(mol3);
	      if (mol3->getAtomDegree(mol3->getLastAtom()) != 1)
		{
		  cerr << "Last atom has more than one connecting bond" <<endl;
		  exit(3);
		}
	      mol1 = mol3;
	      delete_mol1 = true;
	    }
	}
      else
	{
	  map<char,RDKit::ROMol*>::const_iterator smi = a2smi.find(a);
	  if ( smi == a2smi.end())
	    {
	      cerr << "Invalid character in sequence string: "<<a<<" in "<<seq<<endl;
	      exit(3);
	    }
	  mol1 = smi->second;
	}

      

      int i = 0;
      int last = -1;
      for(RDKit::ROMol::AtomIterator a=mol1->beginAtoms(); a!=mol1->endAtoms();++a)
	if ( ++i < mol1->getNumAtoms() || lastmol)
	  mol->addAtom(new RDKit::Atom(**a));
	else
	  last = (*a)->getIdx();
      copy_coordinates(mol,mol1,offset,lastmol);
      offset += mol1->getConformer().getPositions().back();
      if (prior >= 0)
	mol->addBond(prior,n_atom+(*mol1->beginAtoms())->getIdx(),RDKit::Bond::SINGLE);
      for(RDKit::ROMol::BondIterator b=mol1->beginBonds(); b!=mol1->endBonds();++b)
	if ( lastmol || ((*b)->getBeginAtomIdx() != last && (*b)->getEndAtomIdx() != last))
	  mol->addBond(n_atom+(*b)->getBeginAtomIdx(),n_atom+(*b)->getEndAtomIdx(),(*b)->getBondType());
	else
	  prior = n_atom+(*b)->getOtherAtomIdx(last);
	
      n_atom += mol1->getNumAtoms()-1;
      if (!v3000)
	{
	  if (mol->getNumAtoms() > 999)
	    {
	      cerr << "Maximum number of atoms for mol block exceeded: " << seq << endl;
	      exit(3);
	    }
	  if (mol->getNumBonds() > 999)
	    {
	      cerr << "Maximum number of bonds for mol block exceeded: " << seq << endl;
	      exit(3);
	    }
	}
      if (delete_mol1)
	delete mol1;
      m++;
    }
  return mol;
}

string mol_to_sdf(RDKit::RWMol *mol)
{
  string molBlock;
  unsigned int chirality_flag = 1;
  mol->setProp("_MolFileChiralFlag",chirality_flag);
  bool pass = true;
  try 
    {
      RDKit::MolOps::sanitizeMol(*mol);
    } 
  catch(...) 
    {
      cerr << "Cannot sanitize output mol" << endl;
      exit(3);
      pass = false;
    }
  if (pass)
    molBlock=RDKit::MolToMolBlock(*(static_cast<RDKit::ROMol *>(mol)));
  delete mol;
  return molBlock;
}

void add_bond_from_first_to_last(RDKit::RWMol *mol)
{
  int last_atom = (mol->getLastAtom())->getIdx();
  int prior = -1;
  for(RDKit::RWMol::BondIterator b=mol->beginBonds(); b!=mol->endBonds();++b)
    if ((*b)->getBeginAtomIdx() == last_atom || (*b)->getEndAtomIdx() == last_atom)
      prior = (*b)->getOtherAtomIdx(last_atom);
  if (prior > -1)
    {
      mol->removeBond(prior,last_atom);
      mol->removeAtom(last_atom);
      mol->addBond((*mol->beginAtoms())->getIdx(),prior,RDKit::Bond::SINGLE);
    }
}

