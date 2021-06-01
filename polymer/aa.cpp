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

map<char,RDKit::ROMol*> create_map_aa()
{
  map<char,RDKit::ROMol*> m;
  // L-amino acids
  m['A'] = RDKit::SmilesToMol("N[C@@H](C)C(=O)O");
  m['C'] = RDKit::SmilesToMol("N[C@@H](CS)C(=O)O");
  m['D'] = RDKit::SmilesToMol("N[C@@H](CC(=O)O)C(=O)O");
  m['E'] = RDKit::SmilesToMol("N[C@@H](CCC(=O)O)C(=O)O");
  m['F'] = RDKit::SmilesToMol("N[C@@H](Cc1ccccc1)C(=O)O");
  m['G'] = RDKit::SmilesToMol("NCC(=O)O");                     // no stereo for glycine
  m['H'] = RDKit::SmilesToMol("N[C@@H](Cc1c[nH]cn1)C(=O)O"); 
  m['I'] = RDKit::SmilesToMol("N[C@@H]([C@@H](C)CC)C(=O)O"); // two centers
  m['K'] = RDKit::SmilesToMol("N[C@@H](CCCCN)C(=O)O");
  m['L'] = RDKit::SmilesToMol("N[C@@H](CC(C)C)C(=O)O");
  m['M'] = RDKit::SmilesToMol("N[C@@H](CCSC)C(=O)O");
  m['N'] = RDKit::SmilesToMol("N[C@@H](CC(=O)N)C(=O)O");
  m['P'] = RDKit::SmilesToMol("N1CCC[C@H]1C(=O)O"); 
  m['Q'] = RDKit::SmilesToMol("N[C@@H](CCC(=O)N)C(=O)O");
  m['R'] = RDKit::SmilesToMol("N[C@@H](CCCNC(=N)N)C(=O)O");
  m['S'] = RDKit::SmilesToMol("N[C@@H](CO)C(=O)O");
  m['T'] = RDKit::SmilesToMol("N[C@@H]([C@H](O)C)C(=O)O"); // two centers
  m['V'] = RDKit::SmilesToMol("N[C@@H](C(C)C)C(=O)O");
  m['W'] = RDKit::SmilesToMol("N[C@@H](Cc1c[nH]c2ccccc12)C(=O)O"); 
  m['Y'] = RDKit::SmilesToMol("N[C@@H](Cc1ccc(O)cc1)C(=O)O");

  // D-amino acids
  m['a'] = RDKit::SmilesToMol("N[C@H](C)C(=O)O");
  m['c'] = RDKit::SmilesToMol("N[C@H](CS)C(=O)O");
  m['d'] = RDKit::SmilesToMol("N[C@H](CC(=O)O)C(=O)O");
  m['e'] = RDKit::SmilesToMol("N[C@H](CCC(=O)O)C(=O)O");
  m['f'] = RDKit::SmilesToMol("N[C@H](Cc1ccccc1)C(=O)O");
  m['g'] = RDKit::SmilesToMol("NCC(=O)O");                     // no stereo for glycine
  m['h'] = RDKit::SmilesToMol("N[C@H](Cc1c[nH]cn1)C(=O)O"); 
  m['i'] = RDKit::SmilesToMol("N[C@H]([C@H](C)CC)C(=O)O"); // two centers
  m['k'] = RDKit::SmilesToMol("N[C@H](CCCCN)C(=O)O");
  m['l'] = RDKit::SmilesToMol("N[C@H](CC(C)C)C(=O)O");
  m['m'] = RDKit::SmilesToMol("N[C@H](CCSC)C(=O)O");
  m['n'] = RDKit::SmilesToMol("N[C@H](CC(=O)N)C(=O)O");
  m['p'] = RDKit::SmilesToMol("N1CCC[C@@H]1C(=O)O"); 
  m['q'] = RDKit::SmilesToMol("N[C@H](CCC(=O)N)C(=O)O");
  m['r'] = RDKit::SmilesToMol("N[C@H](CCCNC(=N)N)C(=O)O");
  m['s'] = RDKit::SmilesToMol("N[C@H](CO)C(=O)O");
  m['t'] = RDKit::SmilesToMol("N[C@H]([C@@H](O)C)C(=O)O"); // two centers
  m['v'] = RDKit::SmilesToMol("N[C@H](C(C)C)C(=O)O");
  m['w'] = RDKit::SmilesToMol("N[C@H](Cc1c[nH]c2ccccc12)C(=O)O"); 
  m['y'] = RDKit::SmilesToMol("N[C@H](Cc1ccc(O)cc1)C(=O)O");

  add_coordinates(m);
  return m;
}  

map<char,RDKit::ROMol*> create_map_rna()
{
  map<char,RDKit::ROMol*> m;
  // Nucleotide monophosphates
  m['C'] = RDKit::SmilesToMol("OP(O)(=O)(OC[C@H]1O[C@@H](N2C=CC(=NC2=O)N)[C@H](O)[C@@H]1O)");
  m['G'] = RDKit::SmilesToMol("OP(O)(=O)(OC[C@H]1O[C@@H](N2C3=C(N=C2)C(=O)N=C(N3)N)[C@H](O)[C@@H]1O)");
  m['A'] = RDKit::SmilesToMol("OP(O)(=O)(OC[C@H]1O[C@@H](N2C3=C(N=C2)C(=NC=N3)N)[C@H](O)[C@@H]1O)");
  m['U'] = RDKit::SmilesToMol("OP(O)(=O)(OC[C@H]1O[C@@H](N2C=C(C(=O)NC2=O))[C@H](O)[C@@H]1O)");

  add_coordinates(m);
  return m;
}  

map<char,RDKit::ROMol*> create_map_dna()
{
  map<char,RDKit::ROMol*> m;
  // Nucleotide monophosphates
  m['C'] = RDKit::SmilesToMol("OP(O)(=O)(OC[C@H]1O[C@@H](N2C=CC(=NC2=O)N)C[C@@H]1O)");
  m['G'] = RDKit::SmilesToMol("OP(O)(=O)(OC[C@H]1O[C@@H](N2C3=C(N=C2)C(=O)N=C(N3)N)C[C@@H]1O)");
  m['A'] = RDKit::SmilesToMol("OP(O)(=O)(OC[C@H]1O[C@@H](N2C3=C(N=C2)C(=NC=N3)N)C[C@@H]1O)");
  m['T'] = RDKit::SmilesToMol("OP(O)(=O)(OC[C@H]1O[C@@H](N2C=C(C(=O)NC2=O)C)C[C@@H]1O)");

  add_coordinates(m);
  return m;
}  

void delete_map(map<char,RDKit::ROMol*>& m)
{
  for (map<char,RDKit::ROMol*>::iterator i=m.begin(); i!=m.end(); ++i)
    {
      delete i->second;
      i->second = NULL;
    }
  m.clear();
}






