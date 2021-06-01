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

void rotate_molecule(RDKit::RWMol * mol)
{
  RDKit::Conformer &conf =mol->getConformer();
  vector<RDGeom::Point3D>& coords = conf.getPositions();
  RDGeom::Point3D v = coords.back() - coords.front();

  double angle = v.signedAngleTo(RDGeom::Point3D(1,0,0));

  RDGeom::Transform3D t1,t2;
  t1.SetTranslation(-coords.front());
  t2.SetRotation(angle, RDGeom::Z_Axis);
  t2 *= t1; 
   MolTransforms::transformMolsAtoms(mol,t2);
}

void add_coordinates_single(RDKit::RWMol* mol)
{
  try {
    RDKit::MolOps::sanitizeMol(*mol);
  } catch(...) {}
  RDDepict::compute2DCoords(*mol);
  rotate_molecule(mol);
}

void add_coordinates(map<char,RDKit::ROMol*>& m)
{
  for (map<char,RDKit::ROMol*>::iterator i=m.begin(); i!=m.end(); ++i)
    {
      RDKit::RWMol* mol(new RDKit::RWMol(*i->second));
      add_coordinates_single(mol);
      delete i->second;
      i->second = static_cast<RDKit::ROMol *>(mol);
    }
}

void copy_coordinates(RDKit::RWMol* mol, RDKit::ROMol* mol1, RDGeom::Point3D& offset, bool lastmol)
{
  vector<RDGeom::Point3D>& coords = mol1->getConformer().getPositions();
  int length = coords.size();
  if (!lastmol) length--;
  mol->getConformer().resize(mol->getNumAtoms());
  vector<RDGeom::Point3D>& new_coords = mol->getConformer().getPositions();
  int n = new_coords.size();
  for (int i = 0; i < length; i++)
    new_coords[n-length+i] = coords[i]+offset;
}
