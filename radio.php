<?php
// radio.php
header("Content-Type: application/json");

// Pobranie danych z API radia
$apiUrl = "https://radio.projectrpg.pl/statsv2";

// Ustaw nagłówki imitujące przeglądarkę
$options = [
    "http" => [
        "header" => "User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/127.0.0.1\r\n" .
                    "Accept: application/json\r\n" .
                    "Referer: https://radio.projectrpg.pl/\r\n"
    ]
];

$context = stream_context_create($options);

// Pobierz dane
$data = file_get_contents($apiUrl, false, $context);

// Zwróć dane do bota
echo $data;
