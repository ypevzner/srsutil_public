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

inline std::string trim_right( const std::string& s, const std::string& delimiters = " \f\n\r\t\v" )
{
  if (s.empty())   return s;
  return s.substr( 0, s.find_last_not_of( delimiters ) + 1 );
}

inline std::string trim_left(const std::string& s, const std::string& delimiters = " \f\n\r\t\v" )
{
  if (s.empty())   return s;
  return s.substr( s.find_first_not_of( delimiters ) );
}

std::string trim(const std::string& s, const std::string& delimiters )
{
  if (s.empty())   return s;
  return trim_left( trim_right( s, delimiters ), delimiters );
}

vector<string> split(const string &s, char delim) 
{
  vector<string> elems;
  stringstream ss(s);
  string item;
  while (getline(ss, item, delim)) 
    {
      elems.push_back(item);
    }
  return elems;
}

void parse_disulphide_bond(const string& line, map< pair<int,int>, pair<int,int> >& sp)
{
  vector<string> seq_pos = split(line,'-');
  if (seq_pos.size() != 2)
    {
      cerr << "Misformed disulphide bond line entry: " << line << endl;
      exit(2);
    }
  vector<string> first = split(seq_pos[0],'_');
  if (first.size() != 2)
    {
      cerr << "Misformed disulphide bond line entry: " << line << endl;
      exit(2);
    }
  vector<string> second = split(seq_pos[1],'_');
  if (second.size() != 2)
    {
      cerr << "Misformed disulphide bond line entry: " << line << endl;
      exit(2);
    }
  if (first[0].empty() || first[1].empty() || second[0].empty() || second[1].empty())
    {
      cerr << "Misformed disulphide bond line entry: " << line << endl;
      exit(2);
    }
  int seq1 = atoi(first[0].c_str())-1;
  int pos1 = atoi(first[1].c_str())-1;
  int seq2 = atoi(second[0].c_str())-1;
  int pos2 = atoi(second[1].c_str())-1;
  if (seq1 < 0 || pos1 < 0 || seq2 < 0 || pos2 < 0)
    {
      cerr << "Disulfide linkage did not start at 1: " << line << endl;
      exit(2);
    }
  if (seq1 == seq2 && pos1 == pos2)
    {
      cerr << "Disulfide bond links to itself: " << line << endl;
      exit(2);
    }
  pair<int,int> p1(make_pair(seq1,pos1)),p2(make_pair(seq2,pos2));
  if (sp.find(p1) != sp.end() || sp.find(p2) != sp.end())
    {
      cerr << "Disulfide bond listed twice for the same atom: "<<line<<endl;
      exit(2);
    }
  sp[p1] = p2;
  sp[p2] = p1;
}

void add_disulphide_bonds(vector<RDKit::RWMol*>& molecule, map< pair<int,int>, pair<int,int> >& sp, map< pair<int,int>, int >& sp_atoms, bool v3000)
{
  // BFS over disulphide bonds
  vector<bool> exists(molecule.size(),true);
  vector< vector<int> > fragments;
  for (int i=0; i<exists.size(); i++)
    if (exists[i])
      {
	set<int> border;
	set<int> core;
	border.insert(i);
	while (!border.empty())
	  {
	    int j = *(border.begin());
	    for (map< pair<int,int>, pair<int,int> >::iterator k = sp.begin(); k != sp.end(); ++k)
	      if (k->first.first == j && k->second.first >=0 && k->second.first < exists.size() && exists[k->second.first])
		border.insert(k->second.first);
	    core.insert(j);
	    exists[j] = false;
	    border.erase(j);
	  }
	vector<int> v(core.begin(),core.end());
	fragments.push_back(v);
      }
 
  // Shift atom id's
  for (int i=0; i<fragments.size(); i++)
    {
      int offset = 0;
      for (int j=0; j<fragments[i].size(); j++)
	{
	  int s = fragments[i][j];
	  if (j > 0)
	    for (map< pair<int,int>, int>::iterator k=sp_atoms.begin(); k != sp_atoms.end(); k++)
	      if (k->first.first == s)
		k->second += offset;
	  if (s < molecule.size() && molecule[s])
	    offset += molecule[s]->getNumAtoms();
	  else
	    {
	      cerr << "Disulfide bond points to non-existing molecule" << endl;
	      exit(2);
	    }
	}
    }

  // Combine molecules together
  vector<RDKit::RWMol*> new_molecule;
  for (int i=0; i<fragments.size(); i++)
    {
      RDGeom::Point3D y_offset(0,0,0);
      RDKit::RWMol *mol=new RDKit::RWMol();
      RDKit::Conformer *conf = new RDKit::Conformer();
      conf->set3D(false);
      mol->addConformer(conf);
      int n_atom = 0;
      for (int j=0; j<fragments[i].size(); j++)
	{
	  int s = fragments[i][j];
	  RDKit::RWMol *mol1 = molecule[s];
	  for(RDKit::RWMol::AtomIterator a=mol1->beginAtoms(); a!=mol1->endAtoms();++a)
	    mol->addAtom(new RDKit::Atom(**a));
	  for(RDKit::RWMol::BondIterator b=mol1->beginBonds(); b!=mol1->endBonds();++b)
	    mol->addBond(n_atom+(*b)->getBeginAtomIdx(),n_atom+(*b)->getEndAtomIdx(),(*b)->getBondType());
	  vector<RDGeom::Point3D>& coords = mol1->getConformer().getPositions();
	  mol->getConformer().resize(mol->getNumAtoms());
	  vector<RDGeom::Point3D>& new_coords = mol->getConformer().getPositions();
	  int n = new_coords.size();
	  int length = coords.size();
	  for (int i = 0; i < length; i++)
	    new_coords[n-length+i] = coords[i]+y_offset;
	  // Add disulphide bonds
	  for (map< pair<int,int>, pair<int,int> >::iterator b = sp.begin(); b != sp.end(); ++b)
	    if (b->second.first == s && (b->second.first > b->first.first || (b->second.first == b->first.first && b->second.second > b->first.second)))
	    {
	      int a1 = sp_atoms[b->first];
	      int a2 = sp_atoms[b->second];
	      mol->addBond(a1,a2,RDKit::Bond::SINGLE);
	    }
	  n_atom += mol1->getNumAtoms();
	  y_offset += RDGeom::Point3D(0,-10,0); 
	}
      if ((mol->getNumAtoms() <= 999 && mol->getNumBonds() <= 999) || v3000)
	new_molecule.push_back(mol);
      else
	{
	  cerr << "Maximum number of atoms or bonds for mol block exceeded in combining disulphide bonds" << endl;
	  exit(2);
	}
    } 
  for (int i=0; i<molecule.size(); i++)
    delete molecule[i];
  molecule.clear();
  swap(molecule,new_molecule);
}

