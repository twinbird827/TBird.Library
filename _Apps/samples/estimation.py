import lightgbm as lgb
import pandas as pd
from sklearn.metrics import roc_curve,roc_auc_score
import matplotlib.pyplot  as plt
from sklearn.ensemble import RandomForestClassifier
import numpy as np

def split_date(df, test_size):
    sorted_id_list = df.sort_values('日付').index.unique()
    train_id_list = sorted_id_list[:round(len(sorted_id_list) * (1-test_size))]
    test_id_list = sorted_id_list[round(len(sorted_id_list) * (1-test_size)):]
    train = df.loc[train_id_list]
    test = df.loc[test_id_list]
    return train, test


# データの読み込み
data = pd.read_csv('encoded/encoded_data.csv')
#着順を変換
data['着順'] = data['着順'].map(lambda x: 1 if x<4 else 0)

# 特徴量とターゲットの分割
train, test = split_date(data, 0.3)
X_train = train.drop(['着順','オッズ','人気','上がり','走破時間','通過順'], axis=1)
y_train = train['着順']
X_test = test.drop(['着順','オッズ','人気','上がり','走破時間','通過順'], axis=1)
y_test = test['着順']

# LightGBMデータセットの作成
train_data = lgb.Dataset(X_train, label=y_train)
valid_data = lgb.Dataset(X_test, label=y_test)

params={
    'num_leaves':32,
    'min_data_in_leaf':190,
    'class_weight':'balanced',
    'random_state':100
}

lgb_clf = lgb.LGBMClassifier(**params)
lgb_clf.fit(X_train, y_train)
y_pred_train = lgb_clf.predict_proba(X_train)[:,1]
y_pred = lgb_clf.predict_proba(X_test)[:,1]

#モデルの評価
#print(roc_auc_score(y_train,y_pred_train))
print(roc_auc_score(y_test,y_pred))
total_cases = len(y_test)  # テストデータの総数
TP = (y_test == 1) & (y_pred >= 0.5)  # True positives
FP = (y_test == 0) & (y_pred >= 0.5)  # False positives
TN = (y_test == 0) & (y_pred < 0.5)  # True negatives
FN = (y_test == 1) & (y_pred < 0.5)  # False negatives

TP_count = sum(TP)
FP_count = sum(FP)
TN_count = sum(TN)
FN_count = sum(FN)

accuracy_TP = TP_count / total_cases * 100
misclassification_rate_FP = FP_count / total_cases * 100
accuracy_TN = TN_count / total_cases * 100
misclassification_rate_FN = FN_count / total_cases * 100

print("Total cases:", total_cases)
print("True positives:", TP_count, "(", "{:.2f}".format(accuracy_TP), "%)")
print("False positives:", FP_count, "(", "{:.2f}".format(misclassification_rate_FP), "%)")
print("True negatives:", TN_count, "(", "{:.2f}".format(accuracy_TN), "%)")
print("False negatives:", FN_count, "(", "{:.2f}".format(misclassification_rate_FN), "%)")

# True Positives (TP): 実際に1で、予測も1だったもの
# False Positives (FP): 実際は0だが、予測では1だったもの
# True Negatives (TN): 実際に0で、予測も0だったもの
# False Negatives (FN): 実際は1だが、予測では0だったもの

# モデルの保存
lgb_clf.booster_.save_model('model/model.txt')

# 特徴量の重要度を取得
importance = lgb_clf.feature_importances_

# 特徴量の名前を取得
feature_names = X_train.columns

# 特徴量の重要度を降順にソート
indices = np.argsort(importance)[::-1]

# 特徴量の重要度を降順に表示
for f in range(X_train.shape[1]):
    print("%2d) %-*s %f" % (f + 1, 30, feature_names[indices[f]], importance[indices[f]]))