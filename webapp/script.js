// === CONFIG ===
const API_BASE = "https://777bot-dc-webapp-production.up.railway.app";
const API_KEY = "Slots123_twojastara";

// === ICONS ===
const symbols = ["🍒", "🍋", "🍇", "⭐", "💎", "7️⃣"];

// === UI ELEMENTS ===
const r1 = document.getElementById("r1");
const r2 = document.getElementById("r2");
const r3 = document.getElementById("r3");
const spinBtn = document.getElementById("spin");
const resultText = document.getElementById("result");

// === Ensure DOM loaded ===
window.addEventListener("load", async () => {

    console.log("⏳ Waiting for Discord SDK…");

    // Make sure SDK is loaded
    if (typeof Discord === "undefined") {
        console.error("❌ Discord SDK not loaded!");
        resultText.textContent = "❌ Discord SDK failed to load.";
        return;
    }

    console.log("✅ Discord SDK loaded!");

    // Initialize SDK instance
    const sdk = new Discord.EmbeddedAppSdk();
    await sdk.ready();

    console.log("🔗 SDK Ready — Handshake complete.");

    // Fetch user info
    const user = await sdk.commands.getUser();

    if (!user) {
        resultText.textContent = "❌ Cannot load user info!";
        return;
    }

    console.log("👤 Logged user:", user);

    // === Slot Machine Logic ===
    spinBtn.addEventListener("click", async () => {
        resultText.textContent = "";

        // Check balance
        const balRes = await callApi("/check_balance", { userId: user.id });

        if (!balRes || balRes.error) {
            resultText.textContent = "❌ API error checking balance";
            return;
        }

        if (balRes.balance < 5) {
            resultText.textContent = "❌ Not enough credits!";
            return;
        }

        // Charge 5 credits
        const consume = await callApi("/consume", {
            userId: user.id,
            amount: 5
        });

        if (!consume.success) {
            resultText.textContent = "❌ Could not deduct credits!";
            return;
        }

        // Roll slots
        const s1 = symbols[Math.floor(Math.random() * symbols.length)];
        const s2 = symbols[Math.floor(Math.random() * symbols.length)];
        const s3 = symbols[Math.floor(Math.random() * symbols.length)];

        r1.textContent = s1;
        r2.textContent = s2;
        r3.textContent = s3;

        // Check win
        let winAmount = 0;

        if (s1 === s2 && s2 === s3) winAmount = 40;
        else if (s1 === s2 || s2 === s3 || s1 === s3) winAmount = 10;

        if (winAmount > 0) {
            await callApi("/add_balance", {
                userId: user.id,
                amount: winAmount
            });

            resultText.textContent = `🎉 Won +${winAmount} credits!`;
        } else {
            resultText.textContent = "😢 No win this time…";
        }
    });
});

// === API CALL HELPER ===
async function callApi(path, body) {
    try {
        const res = await fetch(API_BASE + path, {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "X-Api-Key": API_KEY
            },
            body: JSON.stringify(body)
        });

        return await res.json();

    } catch (err) {
        console.error("API ERROR:", err);
        return { error: "network_error" };
    }
}
