// Simple Router
function navigateTo(page) {
    if (page === 'logout') {
        localStorage.removeItem('loggedInUser');
        window.location.href = '/';
        return;
    }
    window.location.href = page;
}

// Register Handler
function handleRegister(e) {
    e.preventDefault();
    const name = e.target.querySelector('input[type="text"]').value;
    const email = e.target.querySelector('input[type="email"]').value;
    const password = e.target.querySelector('input[type="password"]').value;
    const btn = e.target.querySelector('button');
    const originalText = btn.innerHTML;

    const users = JSON.parse(localStorage.getItem('users')) || [];
    const userExists = users.find(user => user.email === email);

    if (userExists) {
        alert('Este e-mail já está cadastrado!');
        return;
    }

    users.push({ name, email, password });
    localStorage.setItem('users', JSON.stringify(users));

    btn.disabled = true;
    btn.innerHTML = '<i class="fa-solid fa-circle-notch fa-spin"></i> Cadastrando...';

    setTimeout(() => {
        alert('Cadastro realizado com sucesso! Faça o login.');
        window.location.href = '/Login';
    }, 1000);
}

// Login Handler
function handleLogin(e) {
    e.preventDefault();
    const email = e.target.querySelector('input[type="email"]').value;
    const password = e.target.querySelector('input[type="password"]').value;
    const btn = e.target.querySelector('button');
    const originalText = btn.innerHTML;

    const users = JSON.parse(localStorage.getItem('users')) || [];
    const user = users.find(user => user.email === email && user.password === password);

    if (user) {
        btn.disabled = true;
        btn.innerHTML = '<i class="fa-solid fa-circle-notch fa-spin"></i> Entrando...';

        setTimeout(() => {
            localStorage.setItem('loggedInUser', user.email);
            window.location.href = '/Audio';
        }, 1000);
    } else {
        alert('Usuário ou senha inválidos!');
    }
}

// Check if user is logged in
function checkLogin() {
    const user = localStorage.getItem('loggedInUser');
    if (!user) {
        window.location.href = '/Login';
    }
}

// File Upload Handler
async function handleFileUpload(file) {
    const uploadZone = document.getElementById('upload-zone');
    const processingState = document.getElementById('processing-state');
    const successState = document.getElementById('success-state');
    const progressBar = document.getElementById('progress-bar');
    const percentageText = document.getElementById('percentage-text');
    const statusText = document.getElementById('status-text');
    const step1 = document.getElementById('step-1');
    const step2 = document.getElementById('step-2');
    const step3 = document.getElementById('step-3');

    function setStepActive(el) {
        el.classList.remove('text-gray-500');
        el.classList.add('text-white', 'font-bold');
        el.querySelector('i').className = 'fa-solid fa-circle-notch fa-spin text-brand-purple w-4';
    }
    function setStepDone(el) {
        el.querySelector('i').className = 'fa-solid fa-check text-green-500 w-4';
    }

    // Reset UI
    uploadZone.style.display = 'none';
    processingState.style.display = 'block';
    successState.style.display = 'none';
    progressBar.style.width = '0%';

    const formData = new FormData();
    formData.append('audioFile', file);

    // Simulate progress for UI
    let progress = 0;
    const interval = setInterval(() => {
        progress += Math.random() * 5;
        if (progress > 95) progress = 95; // Don't reach 100% until fetch is done
        progressBar.style.width = `${progress}%`;
        percentageText.innerText = `${Math.round(progress)}%`;

        if (progress < 30) {
            statusText.innerText = "Isolando instrumental...";
            setStepActive(step1);
        } else if (progress < 70) {
            setStepDone(step1);
            setStepActive(step2);
            statusText.innerText = "Sincronizando legendas...";
        } else {
            setStepDone(step2);
            setStepActive(step3);
            statusText.innerText = "Renderizando vídeo...";
        }
    }, 200);

    try {
        const response = await fetch('/Audio/Upload', {
            method: 'POST',
            body: formData,
        });

        clearInterval(interval);
        progressBar.style.width = `100%`;
        percentageText.innerText = `100%`;
        setStepDone(step3);

        if (response.ok) {
            const blob = await response.blob();
            const contentDisposition = response.headers.get('content-disposition');
            let filename = 'karaoke.mp4';
            if (contentDisposition) {
                const filenameMatch = contentDisposition.match(/filename="(.+)"/);
                if (filenameMatch && filenameMatch.length > 1) {
                    filename = filenameMatch[1];
                }
            }

            const downloadUrl = URL.createObjectURL(blob);
            
            document.getElementById('download-filename').innerText = filename;
            document.getElementById('download-filesize').innerText = `${(blob.size / 1024 / 1024).toFixed(2)} MB`;
            document.getElementById('download-button').href = downloadUrl;
            document.getElementById('download-button').download = filename;

            setTimeout(() => {
                processingState.style.display = 'none';
                successState.style.display = 'block';
            }, 600);

        } else {
            const errorText = await response.text();
            alert(`Error: ${errorText}`);
            resetUpload();
        }
    } catch (error) {
        clearInterval(interval);
        alert(`Error: ${error.message}`);
        resetUpload();
    }
}


function resetUpload() {
    document.getElementById('success-state').style.display = 'none';
    document.getElementById('processing-state').style.display = 'none';
    document.getElementById('upload-zone').style.display = 'block';
    document.getElementById('file-upload').value = '';
    // Reset steps style
    [document.getElementById('step-1'), document.getElementById('step-2'), document.getElementById('step-3')].forEach(el => {
        el.className = 'flex items-center gap-3 text-sm text-gray-500';
        el.querySelector('i').className = 'fa-regular fa-circle w-4';
    });
}