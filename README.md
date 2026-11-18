# Screen Translator

Traduz em tempo real o texto exibido na tela (aplicativos e jogos em **janela sem bordas**) e mostra a tradução sobre o original, em um overlay transparente e click-through.

## Instalação
1. Baixe o `Setup.exe` na página **Releases** e execute.
2. Requisitos: Windows 10 64 bits versão 2004 (build 19041) ou superior, com internet.
3. Pacote de OCR do idioma do texto de origem (PowerShell como administrador). Exemplo para inglês:
   `Add-WindowsCapability -Online -Name "Language.OCR~~~en-US~0.0.1.0"`

## Uso
- O app fica na bandeja do sistema (ícone azul "T"). Clique direito para o menu.
- Abra o jogo em **janela sem bordas** (Borderless/Windowed) e use `Ctrl+Alt+R` para selecionar só a área com texto (mais rápido e preciso).

| Atalho | Ação |
|---|---|
| `Ctrl+Alt+T` | Liga/desliga a tradução |
| `Ctrl+Alt+R` | Seleciona a região a traduzir |
| `Ctrl+Alt+F` | Traduz a tela inteira |
| `Ctrl+Alt+L` | Troca o idioma de destino (cicla a lista) |

## Configuração
Arquivo `%AppData%\ScreenTranslator\config.json` (menu da bandeja → *Abrir configurações* → *Recarregar configurações*):
- `Provider`: `google-free` (padrão, sem chave, com cache), `deepl` ou `google` (informe `ApiKey`; chaves DeepL Free terminam em `:fx`).
- `TargetLanguage` (ex.: `pt`, `en`, `es`), `SourceLanguage` (`auto` ou código), `OcrLanguage` (ex.: `en-US`, `ja`, `zh-Hans-CN`, `ru`), `Languages` (lista do atalho de troca), `IntervalMs` (intervalo mínimo entre análises).
- O cache de traduções fica em `cache.json` e o log em `log.txt`, na mesma pasta.

## Compilar
Requisitos: .NET 8 SDK e Inno Setup 6.
```powershell
.\build.ps1 -Version 1.0.0     # gera installer\Output\Setup.exe
```
Para publicar: crie uma release com a tag `v1.0.0` (ou envie a tag); o workflow `release.yml` anexa o `Setup.exe` à release.

## Como funciona e segurança com anti-cheat
Usa somente captura de tela (DXGI Desktop Duplication) + OCR local (Windows.Media.Ocr). **Não injeta código, não lê memória e não toca nos processos dos jogos.** O overlay é uma janela separada, excluída da captura para não ler a própria tradução.

## Limitações
- Não funciona com jogos em tela cheia exclusiva (use janela sem bordas).
- Captura apenas o monitor principal; em notebooks com duas GPUs pode falhar.
- A qualidade do OCR cai com fontes muito estilizadas, texto minúsculo ou fundo animado.
- O serviço gratuito `google-free` é não oficial e pode ter limites ou ser bloqueado; use DeepL/Google com chave para uso intenso.
- O OCR exige o pacote de idioma instalado no Windows; o idioma de origem do OCR é um só por vez.
- Conteúdo protegido contra captura (DRM) aparece em preto.
