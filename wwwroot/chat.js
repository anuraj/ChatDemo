// Chat logic with chat bubbles and backend integration
const chatForm = document.getElementById('chatForm');
const chatInput = document.getElementById('chatInput');
const chatMessages = document.getElementById('chatMessages');
// Track conversationId for server-side chat history
let conversationId = localStorage.getItem('conversationId');
if (!conversationId) {
    conversationId = crypto.randomUUID();
    localStorage.setItem('conversationId', conversationId);
}

function appendMessage(text, sender) {
    const bubbleClass = sender === 'user' ? 'user' : 'bot';
    const msgDiv = document.createElement('div');
    msgDiv.className = sender === 'user' ? 'mb-2 text-end' : 'mb-2 text-start';
    let html = text;
    if (sender !== 'user' && window.marked) {
        html = marked.parse(text);
    } else {
        html = text.replace(/\n/g, '<br>');
    }
    msgDiv.innerHTML = `
        <div class="chat-meta">${sender === 'user' ? 'You' : 'Copilot'}</div>
        <div class="chat-bubble ${bubbleClass}">${html}</div>
    `;
    chatMessages.appendChild(msgDiv);
    chatMessages.scrollTop = chatMessages.scrollHeight;
}

// Show welcome message and recommendations on first load
window.addEventListener('DOMContentLoaded', async () => {
    try {
        const res = await fetch('/welcome');
        if (res.ok) {
            const data = await res.json();
            appendMessage(data.message, 'bot');
            showRecommendations(data.recommendations);
        } else {
            appendMessage('Welcome! How can I help you today?', 'bot');
        }
    } catch {
        appendMessage('Welcome! How can I help you today?', 'bot');
    }
});

chatForm.addEventListener('submit', async function(e) {
    e.preventDefault();
    const msg = chatInput.value.trim();
    if (!msg) return;
    appendMessage(msg, 'user');
    chatInput.value = '';
    chatInput.disabled = true;
    // Show typing indicator
    const typingDiv = document.createElement('div');
    typingDiv.className = 'mb-2 text-start';
    typingDiv.id = 'typing-indicator';
    typingDiv.innerHTML = `<div class='chat-meta'>Copilot</div><div class='chat-bubble bot'><span id='typing-text'>typing<span class='typing-dot'>.</span><span class='typing-dot'>.</span><span class='typing-dot'>.</span></span></div>`;
    chatMessages.appendChild(typingDiv);
    chatMessages.scrollTop = chatMessages.scrollHeight;

    // Animate the typing... message
    let dotCount = 0;
    const typingText = typingDiv.querySelector('#typing-text');
    const interval = setInterval(() => {
        dotCount = (dotCount + 1) % 4;
        typingText.innerHTML = 'typing' + '.'.repeat(dotCount);
    }, 500);

    try {
        const response = await fetch('/chat', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                conversationId,
                message: { role: 'User', content: msg }
            })
        });
        let reply = '';
        if (response.ok) {
            const data = await response.json();
            reply = data.content || '';
        } else {
            reply = 'Error: Unable to get response.';
        }
        typingDiv.remove();
        clearInterval(interval);
        appendMessage(reply, 'bot');
        // Fetch recommendations after bot reply
        try {
            const recResponse = await fetch('/recommendations', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ conversationId })
            });
            if (recResponse.ok) {
                const recommendations = await recResponse.json();
                showRecommendations(recommendations);
            } else {
                showRecommendations([]);
            }
        } catch { showRecommendations([]); }
    } catch (err) {
        typingDiv.remove();
        clearInterval(interval);
        appendMessage('Error: Unable to connect to server.', 'bot');
    } finally {
        chatInput.disabled = false;
        chatInput.focus();
    }
});

function showRecommendations(recommendations) {
    // Remove previous recommendations
    const old = document.getElementById('recommendations-row');
    if (old) old.remove();
    if (!recommendations || recommendations.length === 0) return;
    const row = document.createElement('div');
    row.id = 'recommendations-row';
    row.className = 'd-flex flex-wrap gap-2 mt-3';
    recommendations.forEach(text => {
        const btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'btn btn-outline-primary rounded-pill px-3 py-1';
        btn.textContent = text;
        btn.onclick = () => {
            chatInput.value = text;
            chatInput.focus();
            // Simulate form submit with the recommended text
            chatForm.dispatchEvent(new Event('submit', { cancelable: true }));
        };
        row.appendChild(btn);
    });
    chatMessages.appendChild(row);
    chatMessages.scrollTop = chatMessages.scrollHeight;
}
