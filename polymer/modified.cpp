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
#include <GraphMol/Substruct/SubstructMatch.h>
#include "seq2mol.h"

using namespace std;

RDKit::RWMol* reset_moltab(RDKit::ROMol *m, int first, int last)
{
  RDKit::RWMol *mol=new RDKit::RWMol();
  map<int,int> old_to_new;
  int i = 0;
  old_to_new[first] = i++;
  mol->addAtom(new RDKit::Atom(*m->getAtomWithIdx(first)));
  for(RDKit::ROMol::AtomIterator a=m->beginAtoms(); a!=m->endAtoms();++a)
    if ((*a)->getIdx() != first && (*a)->getIdx() != last)
      {
	mol->addAtom(new RDKit::Atom(**a));
	old_to_new[(*a)->getIdx()] = i++;
      }
  old_to_new[last] = i;
  mol->addAtom(new RDKit::Atom(*m->getAtomWithIdx(last)));

  for(RDKit::RWMol::BondIterator b=m->beginBonds(); b!=m->endBonds();++b)
    mol->addBond(old_to_new[(*b)->getBeginAtomIdx()],old_to_new[(*b)->getEndAtomIdx()],(*b)->getBondType());
  delete m;
  return mol;
}

void create_smarts_patterns_aa(vector<RDKit::ROMol*>& patterns)
{
  RDKit::ROMol *pattern_alpha=RDKit::SmartsToMol("[NX3,NX4+][CX4H]([*])[CX3](=[OX1])[O,N]");  // http://www.daylight.com/dayhtml_tutorials/languages/smarts/smarts_examples.html
  patterns.push_back(pattern_alpha);
}

void create_smarts_patterns_rna(vector<RDKit::ROMol*>& patterns)
{
  RDKit::ROMol *pattern_alpha=RDKit::SmartsToMol("OP(O)(=O)(OC[C@H]1O[C@@H]([*])[C@H](O)[C@@H]1O)");  
  patterns.push_back(pattern_alpha);
}

void create_smarts_patterns_dna(vector<RDKit::ROMol*>& patterns)
{
  RDKit::ROMol *pattern_alpha=RDKit::SmartsToMol("OP(O)(=O)(OC[C@H]1O[C@@H]([*])C[C@@H]1O)");  
  patterns.push_back(pattern_alpha);
}


void delete_smarts_patterns(vector<RDKit::ROMol*>& patterns)
{
   for (int p=0; p<patterns.size(); p++)
     delete patterns[p];
   patterns.clear();
}


void use_smarts_detection(RDKit::ROMol* mol, bool& single, int& first, int& last, vector<RDKit::ROMol*>& patterns)
{
  for (int p=0; p<patterns.size(); p++)
    {
      RDKit::ROMol *pattern = patterns[p];
      vector<RDKit::MatchVectType> matches;
      unsigned int nMatches;
      nMatches=SubstructMatch(*mol,*pattern,matches);
      if (nMatches != 1 || matches.size() != 1 || matches[0].size() != pattern->getNumAtoms()) continue;
      for (int i=0; i<matches[0].size(); i++)
	{
	  if (matches[0][i].first == 0) first = matches[0][i].second;
	  if (matches[0][i].first == pattern->getNumAtoms()-1) last = matches[0][i].second;
	}
    }
  if (first != -1 && last != -1)
    single = false;
}

pair<int,int> get_attachment_points(RDKit::ROMol* nmol, bool& single, vector<RDKit::ROMol*>& patterns)
{
  int first = -1;
  int last = -1;
  if (nmol->hasProp("Attachment_Points"))
    {
      string line;
      nmol->getProp("Attachment_Points",line);
      vector<string> points = split(line,' ');
      if (points.size() == 1)
	{
	  first = atoi(points[0].c_str());
	  single = true;
	}
      else if (points.size() == 2)
	{
	  first = atoi(points[0].c_str());
	  last = atoi(points[1].c_str());
	}
      if (last == -1)
	{
	  if (first != nmol->getNumAtoms()-1)
	    last = nmol->getNumAtoms()-1;
	  else
	    last = 0;
	}
    }
  else
    use_smarts_detection(nmol,single,first,last,patterns);

  if (first < 0 || last < 0)
    {
      cerr << "Cannot parse attachment points in modified molecule " << first <<" " << last << endl;
      exit(102);
    }
  return make_pair(first,last);
}

pair<RDKit::ROMol*,bool> rearrange_mol(RDKit::ROMol* nmol, vector<RDKit::ROMol*>& patterns)
{
  bool single = false;
  pair<int,int> points = get_attachment_points(nmol,single,patterns);
  RDKit::RWMol* mol = reset_moltab(nmol,points.first,points.second);
  RDKit::MolOps::removeHs(*mol);
  add_coordinates_single(mol);
  return make_pair(mol,single);
}

void delete_modified(map<pair<int,int>,pair<RDKit::ROMol*,bool> >& modified_mols)
{
  for (map<pair<int,int>,pair<RDKit::ROMol*,bool> >::iterator i = modified_mols.begin(); i != modified_mols.end(); i++)
    if (i->second.first)
    {
      delete i->second.first;
      i->second.first = NULL;
    }
}


string correct_attachment_point(const string &p, RDKit::ROMol *mol)
{
  int pos;
  std::istringstream(p) >> pos;
  pos--;
  if (pos < 0 || pos >= mol->getNumAtoms() || !(mol->getAtomWithIdx(pos)->getSymbol() == "N" || mol->getAtomWithIdx(pos)->getSymbol() == "C") )
    {
      cerr << "Bad attachment point position: " << pos << endl;
      exit(103);
    }
  if (mol->getAtomWithIdx(pos)->getSymbol() == "C")
    {
      RDKit::ROMol::ADJ_ITER nbrIdx,endNbrs;
      boost::tie(nbrIdx,endNbrs) = mol->getAtomNeighbors(mol->getAtomWithIdx(pos));
      while(nbrIdx!=endNbrs)
	{
          const RDKit::ATOM_SPTR at=(*mol)[*nbrIdx];
	  if ( (at->getTotalDegree() == at->getTotalNumHs() + 1) && ((mol->getBondBetweenAtoms(pos,at->getIdx()))->getBondType() == RDKit::Bond::SINGLE) )
	    {
	      pos = at->getIdx();
	      break;
	    }
          ++nbrIdx;
        }
    }
  std::stringstream str;
  str << pos;
  return str.str();
}
