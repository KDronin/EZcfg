<?php
header('Content-Type: application/json; charset: utf-8');
header('Access-Control-Allow-Origin: *');
header('Access-Control-Allow-Methods: GET, POST');
header('Access-Control-Allow-Headers: Content-Type');
//验证api
$apiKey = isset($_GET['key']) ? trim($_GET['key']) : '';

// 引入数据库验证（通过文件包含）
require_once(__DIR__ . '/private/db_auth.php');

if (!verifyDbCredentials($apiKey)) {
    http_response_code(401);
    die(json_encode(['error' => 'Invalid APIKEY']));
}
$baseUploadDir = 'uploads/';
$maxFileSize = 2 * 1024 * 1024; // 2MB
$allowedFileTypes = ['game.cfg', 'PersistedSettings.json'];

// 获取请求参数
// $apiKey = isset($_GET['key']) ? trim($_GET['key']) : '';
$requestedFile = isset($_GET['file']) ? trim($_GET['file']) : '';

// // 验证API密钥
// if (!isset($apiKeys[$apiKey])) {
//     http_response_code(401);
//     die(json_encode(['error' => '无效的API密钥']));
// }

// 为用户创建专属目录
$userUploadDir = $baseUploadDir . $apiKey . '/';
if (!file_exists($userUploadDir)) {
    mkdir($userUploadDir, 0755, true);
}

// 处理GET请求（获取文件列表或者下载文件）
if ($_SERVER['REQUEST_METHOD'] === 'GET') {
    if (!empty($requestedFile)) {
        // 下载文件请求
        $filePath = $userUploadDir . basename($requestedFile);
        
        if (file_exists($filePath) && in_array($requestedFile, $allowedFileTypes)) {
            header('Content-Description: File Transfer');
            header('Content-Type: application/octet-stream');
            header('Content-Disposition: attachment; filename="'.basename($filePath).'"');
            header('Expires: 0');
            header('Cache-Control: must-revalidate');
            header('Pragma: public');
            header('Content-Length: ' . filesize($filePath));
            readfile($filePath);
            exit;
        } else {
            http_response_code(404);
            die(json_encode(['error' => '文件不存在或不允许下载']));
        }
    } else {
        // 获取文件列表请求
        $files = [];
        if (file_exists($userUploadDir)) {
            foreach (scandir($userUploadDir) as $file) {
                if ($file !== '.' && $file !== '..' && in_array($file, $allowedFileTypes)) {
                    $filePath = $userUploadDir . $file;
                    $files[] = [
                        'Name' => $file,
                        'Size' => formatFileSize(filesize($filePath)),
                        'ModifiedTime' => date('Y-m-d H:i:s', filemtime($filePath))
                    ];
                }
            }
        }
        echo json_encode($files);
        exit;
    }
}

// 处理POST请求（文件上传）
if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    if (!isset($_FILES['file'])) {
        http_response_code(400);
        die(json_encode(['error' => '没有上传文件']));
    }

    $uploadedFile = $_FILES['file'];
    
    // 检查文件类型
    if (!in_array($uploadedFile['name'], $allowedFileTypes)) {
        http_response_code(400);
        die(json_encode(['error' => '不允许的文件类型']));
    }
    
    // 检查文件大小
    if ($uploadedFile['size'] > $maxFileSize) {
        http_response_code(400);
        die(json_encode(['error' => '文件大小超过限制']));
    }
    
    // 检查上传错误
    if ($uploadedFile['error'] !== UPLOAD_ERR_OK) {
        http_response_code(500);
        die(json_encode(['error' => '文件上传错误: ' . $uploadedFile['error']]));
    }
    
    // 移动上传的文件到用户专属目录
    $targetPath = $userUploadDir . basename($uploadedFile['name']);
    if (move_uploaded_file($uploadedFile['tmp_name'], $targetPath)) {
        echo json_encode(['success' => true, 'message' => '文件上传成功']);
    } else {
        http_response_code(500);
        die(json_encode(['error' => '文件保存失败']));
    }
    exit;
}

// 格式化文件大小
function formatFileSize($bytes) {
    if ($bytes >= 1024 * 1024) {
        return round($bytes / 1024 / 1024, 2) . ' MB';
    } elseif ($bytes >= 1024) {
        return round($bytes / 1024, 2) . ' KB';
    } else {
        return $bytes . ' B';
    }
}
?>
