# Digimon Story Cyber Sleuth: Complete Edition — PC Save Editor

Versão para os saves da edição **PC/Steam**, baseada no editor Switch de GreenMii e M1CR0H4CK3R. Edita os **11 slots da party** (três de batalha e oito de reserva) e os campos de personagem, em Cyber Sleuth e Hacker’s Memory. DigiFarm e inventário continuam sem implementação.

## Download — versão 1.2.1

**[Baixar o editor v1.2.1 para Windows (ZIP)](https://github.com/Buloque/Digimon-Story-Cyber-Sleuth-Complete-Edition-PC-Save-Editor/raw/refs/heads/main/releases/DSCS-PC-Save-Editor-v1.2.1.zip)**

O pacote inclui o executável em `bin/`, o código-fonte em `src/` e o resultado dos testes em `validation/`. Requer **.NET Framework 4.7.2 ou superior**. Extraia o ZIP antes de executar o programa.

[Notas da versão e arquivos](https://github.com/Buloque/Digimon-Story-Cyber-Sleuth-Complete-Edition-PC-Save-Editor/releases/tag/v1.2.1) · [Checksum SHA-256](https://github.com/Buloque/Digimon-Story-Cyber-Sleuth-Complete-Edition-PC-Save-Editor/raw/refs/heads/main/releases/SHA256SUMS.txt)

## Versão 1.2.1: limites e gravação dos slots de reserva

- Os slots 4–11 agora usam os mesmos limites dos slots 1–3: níveis até 99, HP até 9.999, SP e a maioria dos stats até 999, DEF até 1.500, bônus de treino até 327, ABI até 200 e CAM até 100.
- Current HP, Current SP e Base SP dos slots de reserva agora são gravados com os 2 bytes usados pelo formato do jogo. A versão 1.2.0 gravava 4 bytes nesses campos e podia sobrescrever os bytes desconhecidos seguintes, fazendo o jogo rejeitar ou zerar valores.
- Saves com um valor preexistente acima do limite da interface, como `102` em um campo limitado a 100, abrem sem erro. O valor é limitado apenas para exibição e os bytes originais permanecem intactos enquanto o campo não for editado.

## Versão 1.2.0: party completa e gênero

- As abas 1–3 são os Digimons de batalha; 4–11 são os oito Digimons de reserva. As duas campanhas oferecem os 11 slots. A versão anterior só tinha interface para os três primeiros e preservava os demais sem mostrá-los.
- Os slots de reserva permitem editar espécie, apelido, stats, equipamentos e as 6 skills atuais e 20 aprendidas. Os bônus de treino aparecem com duas casas decimais. Apenas os campos editados são gravados.
- O seletor **Gender** de Cyber Sleuth agora contém **Male** e **Female**. Antes a lista estava vazia. A seleção grava o campo `playerSex` completo (32 bits) e é restaurada ao reabrir o save.
- Hacker’s Memory tem protagonista fixo e não oferece esse seletor. A troca visual de personagem dentro do jogo ainda não foi verificada; não alteramos campos de modelo/roupa sem evidência do save real.

## Como usar no Windows

1. Extraia o ZIP e execute `bin/DigimonStorySaveEditor.exe` com .NET Framework 4.7.2 ou superior.
2. Feche o jogo e abra uma **cópia** do save numerado, por exemplo `0000.bin`, pelo menu File → Open. Não abra `slot_0000.bin` nem `system_data.bin`.
3. Edite os campos e escolha File → Save. O arquivo é criptografado novamente no formato PC; o primeiro original fica em `0000.bin.bak`. Backups existentes nunca são sobrescritos.
4. Coloque o save editado no local original para testar no jogo. Os arquivos `slot_*.bin` e `system_data.bin` não são alterados por este editor.

O save numerado suportado tem **843.568 bytes**. Arquivos Switch e outros tamanhos são rejeitados. A descriptografia e a conversão de layout ocorrem em memória; abrir um arquivo não cria nem altera arquivos. A gravação usa substituição do arquivo após preparar o conteúdo completo e recusa sobrescrever um save que mudou no disco desde a abertura.

### Slots vazios

Desmarcar “None” copia um Digimon existente **da mesma party**, incluindo os campos que o editor não conhece. Depois é possível mudar espécie, stats e skills. Marcar “None” remove o ocupante sem apagar seu bloco de dados. Trocar a espécie atualiza os retratos; os três slots de batalha de cada campanha também mantêm seus sprites.

A cópia exige pelo menos dois ocupantes e um único campo do cabeçalho que corresponda às posições de todos os ocupantes nos 11 slots da party. Só então o índice é reescrito para a posição de destino. Se não houver evidência suficiente, a criação é recusada; a edição dos ocupantes existentes continua funcionando.

**A criação por cópia é experimental:** o identificador interno do Digimon em +0x0C também é copiado. Sua necessidade de unicidade ainda não foi confirmada. A aceitação da nova party e a tela de informação precisam ser verificadas dentro do jogo com um save real.

### Preservação dos dados

O editor compara cada campo com sua representação inicial na interface e escreve somente campos alterados. Assim, valores arredondados na tela (como bônus de HP/SP em centésimos), regiões desconhecidas, slots vazios e nomes não editados não são normalizados durante uma gravação. Os nomes são lidos e escritos como UTF-8 nos buffers completos (72 bytes para apelido, 40 para personagem), reservando um byte terminador ao editar.

## Compilar e testar

No Windows, instale Visual Studio 2019/2022 com a carga **Desenvolvimento para desktop com .NET** e o **Developer Pack do .NET Framework 4.7.2**. Abra `DigimonStorySaveEditor.sln` e compile em Release. A referência ao `GlobalSuppressions.cs` ausente foi removida. Não há dependências NuGet para executar o editor.

No Developer Command Prompt do Visual Studio:

```bat
msbuild DigimonStorySaveEditor.sln /p:Configuration=Release
msbuild tests\SaveEditorTests.csproj /p:Configuration=Release
tests\bin\Release\SaveEditorTests.exe
```

No ambiente de nuvem preparado pelo onboarding:

```sh
bash scripts/test-cloud.sh
```

O script usa Mono 6.12, Roslyn 4.8 e Xvfb instalados em `/workspace/.cloud-setup`. Saídas de compilação e testes ficam fora do checkout. O aviso de toolset legado do xbuild permanece; a compilação usa Roslyn e as referências .NET do Mono.

### Validação realizada

- Criptografia **e** descriptografia comparadas à implementação C++ do MVGLTools em 12 entradas, incluindo zeros, bytes 0–255 e dados pseudoaleatórios. Um vetor de referência está fixado na suíte.
- 34 testes automatizados com saves sintéticos: limites dos oito intervalos de layout, abertura/gravação sem mudanças idêntica byte a byte, edições isoladas nas duas campanhas, nomes japoneses, bônus fracionários, backups, conflito com alterações externas, retratos, party completa nas duas campanhas, limites e larguras dos campos dos 16 slots de reserva, cópia/remoção até o slot 11, gênero, recarga de arquivos, valores preexistentes fora do limite e reversão de edições após salvar.
- Abertura e fechamento da interface verificados sob Mono/Xvfb.

**A versão 1.2.1 foi validada com saves sintéticos e Mono/Xvfb no Linux. Ainda falta validação com o `0002.bin` que apresentou o erro, dentro do jogo e em execução nativa no Windows.** A leitura e a gravação verificadas pelos testes não garantem a aceitação de todas as edições pelo jogo.

## Formato e fontes

A cifra foi portada de [MVGLTools](https://github.com/SydMontague/MVGLTools/blob/089c2497be58acc3a052fe569854ad198d1ecf2a/MVGLTools/SaveFile.cpp), de SydMontague, sob BSD-3-Clause. A licença está em `THIRD-PARTY-NOTICES.txt` e acompanha o binário.

Os oito intervalos Switch ausentes no PC são de quatro bytes, nos offsets anteriores à remoção:

```
0x4BA0C  0x4BA9C  0x4BACC  0x4BB0C
0xAD1AC 0xAD23C 0xAD26C 0xAD2AC
```

Referência acessível: [conversor publicado por GiowGiow](https://gist.github.com/GiowGiow/fb0010c3d3f2842cf2fd2185332ed423), indicado na [issue #30 do MVGLTools](https://github.com/SydMontague/MVGLTools/issues/30). Utilizamos os offsets e tamanhos documentados; o script de terceiros não é executado nem incluído no aplicativo. Expandir esses intervalos em memória permite reaproveitar os offsets Switch; eles são removidos novamente antes de criptografar. O save expandido tem 843.600 bytes.

Os tamanhos de nomes e do bloco Digimon também foram conferidos nas estruturas [Savegame.h](https://github.com/SydMontague/DSCSModLoader/blob/master/include/dscs/Savegame.h), [DigimonContext.h](https://github.com/SydMontague/DSCSModLoader/blob/master/include/dscs/GameContext/DigimonContext.h) e [Player.h](https://github.com/SydMontague/DSCSModLoader/blob/master/include/dscs/GameContext/Player.h) do DSCSModLoader.

# Original Credits
```
Huge thanks to these folks at GBAtemp: AnalogMan for data research and awesome script, omega7 for data research, supermariorick and ukee for the Edizon script and data research.

Also thanks to DrummerIX on FearLess Cheat Engine for the cheat table that I cross-referenced and segamot and dd777 for updating it with more stats.

Big thanks to digidb.io from which I got all the Digimon portraits.

Finally, shoutouts to Snodeca for her Dream Team save editor that I referenced when making this one!
```

## Additional Credits:

Thanks to GreenMii for laying the ground work, and JoelCoehoorn on StackOverflow for the help refactoring a function that was causing longer than desired load times. Also thanks to Draken70 at GameFAQs for the awesome walkthrough for the first game that in particular had a (mostly correct) rank up table to reference.
