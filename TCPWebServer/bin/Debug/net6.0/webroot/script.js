// webroot/script.js
function testJavaScript() {
    const resultDiv = document.getElementById('js-result');
    const currentTime = new Date().toLocaleString();
    
    resultDiv.innerHTML = `
        <h3>JavaScript Test Successful!</h3>
        <p><strong>Server Time:</strong> ${currentTime}</p>
        <p><strong>User Agent:</strong> ${navigator.userAgent}</p>
        <p><strong>Page URL:</strong> ${window.location.href}</p>
        <p>✅ TCP Web Server is successfully serving JavaScript files!</p>
    `;
    
    resultDiv.classList.add('show');
    
    // Add some interactive behavior
    setTimeout(() => {
        resultDiv.style.background = '#e6fffa';
        resultDiv.style.borderColor = '#38b2ac';
    }, 500);
}

// Add some interactive behavior when the page loads
document.addEventListener('DOMContentLoaded', function() {
    console.log('TCP Web Server - JavaScript loaded successfully!');
    
    // Add click animations to navigation links
    const navLinks = document.querySelectorAll('nav a');
    navLinks.forEach(link => {
        link.addEventListener('click', function(e) {
            // Add a small delay for the animation effect
            this.style.transform = 'scale(0.95)';
            setTimeout(() => {
                this.style.transform = 'translateY(-2px)';
            }, 100);
        });
    });
    
    // Add a welcome message
    setTimeout(() => {
        console.log('Welcome to the TCP Web Server! All files loaded successfully.');
    }, 1000);
});