// Requires sharp (npm install --no-save sharp, or expose it through NODE_PATH).
// SVG sources are authoritative; commit the generated PNGs for dependency-free app builds.
const fs = require('node:fs');
const path = require('node:path');
const sharp = require('sharp');
const root = path.resolve(__dirname, '..');
async function main() {
  const source = path.join(root, 'mockups/pet/svg');
  for (const name of fs.readdirSync(source).filter(n => /^pet_(save_|thinking_).*\.svg$/.test(n))) {
    await sharp(path.join(source, name), { density: 192 }).resize(320, 320)
      .png().toFile(path.join(root, 'src/ZomniverseGitPet/Assets/Pet', name.replace('.svg', '.png')));
  }
}
main().catch(error => { console.error(error); process.exitCode = 1; });
