const fs=require('node:fs'),path=require('node:path');const root=path.resolve(__dirname,'..');
const actions=JSON.parse(fs.readFileSync(path.join(root,'actions.json'),'utf8'));
const js=fs.readFileSync(path.join(root,'web/app.js'),'utf8');
const labels=JSON.parse(js.match(/Object\.assign\(PERM_LABELS,(\{[^\n]+\})\);/)[1]);
let text='# Einstufung der PC-Extras\n\nDiese Liste wird aus den tatsächlich verwendeten Kategorien erzeugt. 1 = leicht, 2 = mittel, 3 = schwer; 0 = ohne Cooldown. Die eingestellte Kategoriezeit gilt pro Benutzer und einzelner Funktion.\n';
for(const [category,title] of [[1,'Leicht'],[2,'Mittel'],[3,'Schwer'],[0,'Ohne Cooldown']]){
 const list=actions.filter(a=>a.category===category&&labels[a.id]);text+='\n## '+title+'\n\n';
 text+=list.length?list.map(a=>'- '+labels[a.id][1]+' (`'+a.id+'`)').join('\n')+'\n':'Keine der zusätzlichen PC-Funktionen. Die übernommenen Funktionen wie Herunterfahren, Filter Keys oder Explorer-Flut bleiben weiterhin schwer.\n';
}
const file=path.join(root,'../docs/pc-extra-funktionsklassen.md');fs.writeFileSync(file,text);console.log('Exported '+Object.keys(labels).length+' extra classifications.');
