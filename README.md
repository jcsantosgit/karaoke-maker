
# 🎤 Karaoke Master Pro 🎶

[![Build Status](https://img.shields.io/badge/build-passing-brightgreen)](https://github.com/joao/karaoke-project)
[![License](https://img.shields.io/badge/license-MIT-blue)](https://github.com/joao/karaoke-project/blob/main/LICENSE)
[![.NET Version](https://img.shields.io/badge/.NET-7.0-blueviolet)](https://dotnet.microsoft.com/download/dotnet/7.0)

<p align="center">
  <img src="https://media.giphy.com/media/v1.Y2lkPTc5MGI3NjExa3g1c2wzYjZkN2wzY3g1c2wzYjZkN2wzY3g1c2wzYjZkN2wzY3g1YyZlcD12MV9pbnRlcm5hbF9naWZfYnlfaWQmY3Q9Zw/3o7btXIel4aI7B421a/giphy.gif" alt="Karaoke Animation" width="400"/>
</p>

<p align="center">
  <strong>Transforme qualquer música em um karaokê com letras sincronizadas!</strong>
</p>

---

## 🌟 Sobre o Projeto

O **Karaoke Master Pro** é uma aplicação web inovadora que permite aos usuários fazer o upload de suas músicas favoritas e, através de um processo de transcrição de áudio, gera automaticamente as letras e as sincroniza com a música, criando uma experiência de karaokê personalizada.

Este projeto foi construído com o objetivo de fornecer uma ferramenta fácil de usar e poderosa para amantes de karaokê, utilizando tecnologias de ponta para processamento de áudio e reconhecimento de voz.

---

## ✨ Funcionalidades

- **🎤 Upload de Músicas:** Faça o upload de arquivos de áudio e vídeo em diversos formatos.
- **🤖 Transcrição Automática:** Utiliza o modelo Whisper para transcrever as letras das músicas com alta precisão.
- **🎶 Sincronização de Letras:** As letras são sincronizadas com o áudio para uma experiência de karaokê perfeita.
- **👤 Autenticação de Usuários:** Sistema de registro e login para gerenciar suas músicas.
- **🌐 Interface Web Moderna:** Uma interface de usuário amigável e responsiva para uma ótima experiência.
- **🐳 Suporte a Docker:** Facilidade de deployment com a utilização de contêineres Docker.

---

## 🛠️ Tecnologias Utilizadas

Este projeto foi desenvolvido utilizando as seguintes tecnologias:

- **Backend:** ASP.NET Core 7
- **Frontend:** HTML, CSS, JavaScript, Bootstrap
- **Banco de Dados:** (Não especificado, pode ser configurado)
- **Processamento de Áudio/Vídeo:** FFmpeg (através da biblioteca Xabe.FFmpeg)
- **Transcrição de Áudio:** Whisper.net
- **Servidor Web:** Nginx
- **Containerização:** Docker

---

## 🚀 Começando

Para executar o projeto em seu ambiente local, siga os passos abaixo.

### Pré-requisitos

- [.NET 7 SDK](https://dotnet.microsoft.com/download/dotnet/7.0)
- [Docker](https://www.docker.com/get-started) (recomendado)

### Instalação com Docker

1.  Clone o repositório:
    ```sh
    git clone https://github.com/joao/karaoke-project.git
    ```
2.  Navegue até o diretório do projeto:
    ```sh
    cd karaoke-project
    ```
3.  Construa e execute o contêiner Docker:
    ```sh
    docker-compose up --build
    ```
4.  Acesse a aplicação em `http://localhost:8080`.

### Instalação Local

1.  Clone o repositório:
    ```sh
    git clone https://github.com/joao/karaoke-project.git
    ```
2.  Navegue até o diretório da aplicação:
    ```sh
    cd karaoke-project/src/KaraokeApp
    ```
3.  Restaure as dependências do .NET:
    ```sh
    dotnet restore
    ```
4.  Execute a aplicação:
    ```sh
    dotnet run
    ```
5.  Acesse a aplicação em `http://localhost:5000` ou `https://localhost:5001`.

---

##  usage Uso

1.  **Registre-se:** Crie uma nova conta ou faça login se já tiver uma.
2.  **Faça o Upload:** Vá para a página de upload e envie sua música.
3.  **Aguarde o Processamento:** A aplicação irá processar o áudio e transcrever a letra.
4.  **Cante!** Acesse sua música na sua biblioteca e comece a cantar com a letra sincronizada.

---

## 🤝 Contribuindo

Contribuições são o que tornam a comunidade de código aberto um lugar incrível para aprender, inspirar e criar. Qualquer contribuição que você fizer será **muito apreciada**.

1.  Faça um Fork do projeto
2.  Crie sua Feature Branch (`git checkout -b feature/AmazingFeature`)
3.  Faça o Commit de suas mudanças (`git commit -m 'Add some AmazingFeature'`)
4.  Faça o Push para a Branch (`git push origin feature/AmazingFeature`)
5.  Abra um Pull Request

---

## 📄 Licença

Distribuído sob a licença MIT. Veja `LICENSE` para mais informações.

---

## 📬 Contato

João - [@seu_twitter](https://twitter.com/seu_twitter) - email@example.com

Link do Projeto: [https://github.com/joao/karaoke-project](https://github.com/joao/karaoke-project)
