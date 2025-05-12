<?php
header('Content-Type: application/json; charset=utf-8');
// 数据库配置
$dbConfig = [
    'host' => '11.22.33.44',
    'database' => 'www_abcd_com',
    'username' => 'www_abcd_com',
    'password' => '123456',
    'charset' => 'utf8mb4'
];
// 验证函数
function verifyDbCredentials($inputKey, $dbConfig = null) {
    // 当没有传入$dbConfig，使用全局变量
    if ($dbConfig === null) {
        global $dbConfig;
    }
    try {
        $pdo = new PDO(
            "mysql:host={$dbConfig['host']};dbname={$dbConfig['database']};charset={$dbConfig['charset']}",
            $dbConfig['username'],
            $dbConfig['password'],
            [
                PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION,
                PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC
            ]
        );
        // 查询数据库中的用户密码（作为API Key）
        $stmt = $pdo->prepare("SELECT USERPSWD FROM users WHERE USERPSWD = ?");//users和USERPAWD根据数据库实际情况修改
        $stmt->execute([$inputKey]);
        return $stmt->fetch() !== false;
    } catch (PDOException $e) {
        error_log("数据库错误: " . $e->getMessage());
        return false;
    }
}
?>
