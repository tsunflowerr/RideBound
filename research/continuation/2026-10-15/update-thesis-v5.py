from pathlib import Path
import hashlib, json, re

ROOT = Path('C:/RideBoundContinuation/2026-10-15/thesis-v5')
BASE = Path('C:/RideBoundContinuation/2026-10-15/thesis-v4')
changes = {}

def load(name):
    text = (ROOT/'noi-dung'/name).read_text(encoding='utf-8')
    assert text == (BASE/'noi-dung'/name).read_text(encoding='utf-8'), name
    return text

def once(text, old, new):
    assert text.count(old) == 1, old[:130]
    return text.replace(old, new, 1)

def between(text, first, last, new):
    assert text.count(first) == text.count(last) == 1
    a, b = text.index(first), text.index(last)
    return text[:a] + new + '\n\n' + text[b:]

abstract = r'''% Cập nhật ngày 15/10/2026. Nguồn mới: results/stage1b-report.txt, mục 2, 5.
\chapter*{Tóm tắt}
\addcontentsline{toc}{chapter}{Tóm tắt}

Trong dịch vụ đi chung xe, giờ trả đã hứa có thể bị dời khi bộ điều phối ghép thêm khách hoặc khi thời gian đi thay
đổi. Khóa luận đặc tả và cài đặt luật C trong RideBound: tổng độ lớn phần dời giờ trả tính cho điều phối của mỗi
khách trong cả chuyến không vượt một trần cố định. Mỗi lần dời được đo so với giờ trả tính lại khi giữ nguyên kế
hoạch cũ, mốc đã có ở Hörl và Zwick. Luật C đi cùng lời hứa theo dự báo tắc đường và sổ theo từng khách; sổ phân
biệt phần tính cho điều phối, phần được dự báo giải thích và phần còn lại.
% EV: A01; A06; X3-12; J01; J03; J35. Giới hạn có điều kiện ở muc:giunguyen.

Phép đo dùng FleetPy trên 34 kịch bản Manhattan, với 3.672 yêu cầu mỗi cấu hình. Khóa luận so hai cách chọn phương
án: ưu tiên ổn định và ưu tiên chi phí kế hoạch. Khi cùng giữ trần 30 giây, cách thứ hai phục vụ 2.364 thay vì 2.338
lượt và có 432 thay vì 313 khách hoàn thành chuyến có đi chung xe. Đổi lại, số khách bị sửa giờ trả tăng từ 33 lên
106. Không khách nào vượt trần ở cả hai cách chọn. Với cách ưu tiên chi phí, bỏ trần tăng số lượt phục vụ từ 2.364
lên 2.502, nhưng số lượt trễ hơn lời hứa đầu chưa tính dự báo quá một phút tăng từ 72 lên 379. Các nhóm khách được
phục vụ khác nhau, nên các chênh lệch mô tả đánh đổi giữa các cấu hình.
% EV: S15-01 (report mục 2, 5d--5f); S15-02 (kiểm lại số từ trích xuất).

Khi tính lại lời hứa có dự báo trên hành trình đã ghi của cấu hình ưu tiên chi phí có trần, số lượt trễ quá một
phút là 11 trong 2.364 lượt phục vụ. Đây là điều chỉnh giờ hứa, không làm xe chạy nhanh hơn. Đối chứng hạn chót
và đệm lời hứa cho thấy luật C không vượt trội ở mọi thước đo. Các kết quả có tính thăm dò, chỉ áp cho mô phỏng tải
thấp với hệ số tắc đường chung toàn thành phố. Khóa luận chưa chọn một trần tối ưu hoặc xác nhận mức hài lòng của
khách; phần dự báo dùng hồ sơ lịch sử đơn giản, chưa phải một mô hình dự báo giao thông mới.
% EV: S15-01 (5b); C01; C10; K05; K36; I01; I05; F01--F04.

\vspace{6pt}
\noindent\textbf{Từ khóa:} điều phối xe đi chung, lời hứa giờ trả, trần theo từng khách, lời hứa theo dự báo,
sổ theo từng khách, mô phỏng FleetPy.
\clearpage

\chapter*{Abstract}
\addcontentsline{toc}{chapter}{Abstract}

In ride-pooling, promised drop-off times may change when a dispatcher inserts riders or travel times change.
This thesis specifies and implements rule C in RideBound: each rider has a fixed cap on the sum of the magnitudes
of dispatch-charged drop-off changes over the trip. Each change is measured against the updated drop-off time
under the existing plan, a reference already used by Hörl and Zwick. Rule C is combined with forecast-based
promises and a per-rider ledger separating dispatch-charged changes, the part explained by the forecast, and the
remainder.

Measurements use FleetPy on 34 Manhattan scenarios, with 3,672 requests per configuration. Two selection orders
are compared: stability-first and planned-cost-first. Under the same 30-second cap, planned-cost-first serves
2,364 rather than 2,338 riders, and 432 rather than 313 served riders share a vehicle. In exchange, the number
whose drop-off promise is revised rises from 33 to 106. Neither capped configuration exceeds its dispatch-change
budget. Under planned-cost-first, removing the cap raises service from 2,364 to 2,502, but arrivals more than one
minute after the initial unadjusted promise rise from 72 to 379. Served populations differ, so these are
descriptive configuration trade-offs.

Replaying forecast-based promises on the capped planned-cost-first trajectories leaves 11 of 2,364 served
riders more than one minute late. This changes the quoted time, not vehicle movement. Deadline and padding
comparisons show that rule C does not dominate every measure. The findings are exploratory and limited to the
low-load simulation with city-wide traffic factors. No optimal budget or passenger-satisfaction claim is made;
the forecast uses a simple historical profile rather than a new traffic-prediction model.

\vspace{6pt}
\noindent\textbf{Keywords:} ride-pooling dispatch, promised drop-off time, per-rider cap, forecast-based promise,
per-rider ledger, FleetPy simulation.
\clearpage
'''
load('02-tom-tat.tex')
changes['02-tom-tat.tex'] = abstract

c1 = load('05-chuong1.tex')
c1 = once(c1, r'''\noindent\textbf{Mục tiêu.} Khóa luận nhắm tới một giới hạn hãng xe có thể kiểm lại cho từng khách: từ khi
xác nhận đặt xe, tổng các lần điều phối dời giờ trả không vượt một mức cố định, dù dự báo đúng hay sai.
Cái giá được đo bằng số lượt phục vụ, số khách đi chung và độ dài lời hứa.''', r'''\noindent\textbf{Mục tiêu.} Khóa luận nhắm tới một giới hạn có thể kiểm lại cho từng khách: tổng độ lớn phần dời
giờ trả tính cho điều phối trong cả chuyến không vượt một mức cố định. Giới hạn không phụ thuộc dự báo đúng hay
sai, nhưng cần điều kiện giữ nguyên kế hoạch ở mục~\ref{muc:giunguyen}. Cái giá được đo bằng số lượt phục vụ,
số khách đi chung, số khách bị sửa giờ trả và độ dài lời hứa.''')
c1 = once(c1, '3.672 yêu cầu mỗi cấu hình. Điều phối không trần phục vụ', '3.672 yêu cầu mỗi cấu hình. Với bộ chọn ưu tiên ổn định, điều phối không trần phục vụ')
c1 = once(c1, 'trần loại. Đây không phải ưu thế giữ giờ tới;', 'trần loại. Luật phục hồi là cách tiếp tục phục vụ khi luật lời hứa loại phương án giữ nguyên, nêu ở\nmục~\\ref{muc:doichung}. Đây không phải ưu thế giữ giờ tới;')
insert1 = r'''\paragraph{Vai trò của cách chọn phương án.}
Bộ chọn ưu tiên ổn định xét phần dời giờ trước chi phí kế hoạch. Vì vậy, khi chỉ dùng cách chọn này, chưa rõ kết
quả ít sửa giờ còn giữ được nếu bộ điều phối chuyển sang ưu tiên chi phí. Phép so ở mục~\ref{muc:c4-vaitro} trả
lời câu hỏi đó bằng cách xét cả có trần và không trần dưới hai thứ tự ưu tiên. Khi cùng giữ trần 30 giây, ưu tiên
chi phí phục vụ thêm 26 lượt và có thêm 119 khách hoàn thành có đi chung xe; số khách bị sửa giờ trả cũng tăng từ
33 lên 106. Hai cách chọn thể hiện hai mức đánh đổi, không đủ để gọi một cách tốt hơn ở mọi tiêu chí.
% EV: S15-01 (mục 2, 5d--5f); S15-02. Không diễn giải thành tác động nhân quả riêng của bộ chọn.

'''
c1 = once(c1, '\\begin{khungy}\n\\textbf{Khóa luận không nhận là mới.}', insert1 + '\\begin{khungy}\n\\textbf{Khóa luận không nhận là mới.}')
c1 = once(c1, r'''điều phối không trần dùng cùng cách chọn phương án, chỉ khác trần giờ trả. Chưa chạy mô phỏng đầy đủ
với thứ tự chọn ưu tiên chi phí, nên chưa biết đánh đổi còn giữ khi đổi cách chọn hay không.''', r'''điều phối không trần được so dưới cả thứ tự ưu tiên ổn định và ưu tiên chi phí kế hoạch. Trong mỗi thứ tự,
hai cấu hình giữ cùng các ràng buộc còn lại và khác ở trần giờ trả. Phép so theo thứ tự ưu tiên mới xét ở trần
30 giây và không trần, chưa xác định ngưỡng thích hợp cho người dùng.''')
changes['05-chuong1.tex'] = c1

c3 = load('07-chuong3.tex')
c3 = once(c3, r'''bộ chọn, chưa được kiểm với bộ chọn ưu tiên chi phí (mục~\ref{muc:c4-vaitro}).''', r'''bộ chọn. Khóa luận còn xét thứ tự ưu tiên chi phí: giữ ưu tiên nhận yêu cầu và các ràng buộc, bỏ các mức xếp
hạng theo ổn định rồi so chi phí kế hoạch và tiêu chí phân xử. Phương án vẫn phải qua phép kiểm trần khi trần
được bật. Mục~\ref{muc:c4-vaitro} so hai thứ tự ở trần 30 giây và không trần; đổi thứ tự không làm thay đổi
định nghĩa hay cách cộng trần của luật C.
% EV: S15-03 (costfirst-diff, cấu hình và provenance); S15-01 (mục 0, 2).''')
c3 = once(c3, r'''phải cho đúng kết quả cũ trên một số kịch bản trước khi dùng (Phụ lục~\ref{phuluc:C}).''', r'''phải đối chiếu với kết quả đã lưu trước khi dùng. Các lượt ưu tiên chi phí dùng bản riêng; bốn cặp kiểm
hành vi không thay thế việc kiểm đồng nhất toàn bộ hai bản chạy (Phụ lục~\ref{phuluc:C}).''')
changes['07-chuong3.tex'] = c3

c4 = load('08-chuong4.tex')
start = r'\section{Vai trò của trần và thứ tự tiêu chí của bộ chọn}'
end = r'\section{Lời hứa theo dự báo}'
old_section = c4[c4.index(start):c4.index(end)]
screen = old_section[old_section.index('Bằng chứng hiện có chỉ là phép sàng lọc'):old_section.index('Phép sàng lọc không cho biết số lượt phục vụ')]
screen = screen.replace('Bằng chứng hiện có chỉ là phép sàng lọc', 'Phép sàng lọc bổ trợ được thực hiện')

new_section = r'''\section{Vai trò của trần và thứ tự tiêu chí của bộ chọn}
\label{muc:c4-vaitro}

\subsection{Vì sao cần đổi thứ tự ưu tiên?}

Các kết quả trước mục này dùng bộ chọn ưu tiên ổn định: sau khi xét số yêu cầu nhận được, bộ chọn so mức dùng
trần và phần dời giờ rồi mới xét chi phí kế hoạch. Như vậy, cả việc xếp hạng phương án lẫn trần cứng đều có thể
góp phần hạn chế thay đổi lời hứa. Chỉ so có trần với không trần dưới một thứ tự chưa cho biết kết quả sẽ ra sao
khi bộ điều phối đặt chi phí kế hoạch lên trước phần dời giờ.

Để kiểm tra điều đó, khóa luận giữ ưu tiên nhận yêu cầu và các phép kiểm khả thi, nhưng cho bộ chọn xét chi phí
kế hoạch ngay sau đó. Các mức xếp hạng theo ổn định được bỏ, còn tiêu chí phân xử theo mã định danh giữ nguyên.
Trần cứng của luật C vẫn được kiểm trước khi chọn nếu cấu hình có trần. Hai cách gọi dưới đây là \emph{ưu tiên
ổn định} và \emph{ưu tiên chi phí kế hoạch}; chúng biểu thị mục tiêu khác nhau, không phải hai mức chất lượng
mặc định. Chi phí kế hoạch là tổng thời gian còn lại của tuyến dự kiến, chưa phải tiền hay chi phí vận hành thực.
% EV: S15-03; A14; X3-07; K15. Cùng ưu tiên nhận khách trong từng lượt, không bảo đảm cùng số phục vụ cuối.

Phép so gồm bốn cấu hình trên cùng 34 kịch bản và 3.672 yêu cầu mỗi cấu hình: hai thứ tự ưu tiên, mỗi thứ tự có
trần 30 giây và không trần. Hai cấu hình ưu tiên chi phí có 68 lượt mô phỏng hoàn tất, được đối chiếu với 68 lượt
ưu tiên ổn định đã lưu. Tập kịch bản và tập mã yêu cầu khớp ở cả 34 kịch bản. Các diễn biến sau khi chọn có thể
khác nhau, nên nhóm khách được phục vụ cũng khác nhau.
% EV: S15-01 (mục 0--3), main-final-admission.json; S15-02. Không có 34 lần lặp độc lập.

\subsection{Đánh đổi giữa phục vụ, ghép khách và sửa lời hứa}

\begin{table}[htbp]
\centering
\caption[Đánh đổi dưới hai thứ tự chọn phương án]{Hai thứ tự chọn phương án trên tập kiểm tra chính, mỗi cấu hình
có 3.672 yêu cầu. Trễ được so với lời hứa trả đầu chưa tính dự báo, trừ dòng ghi riêng. Khách có đi chung được
đếm một lần nếu hoàn thành chuyến và có khoảng thời gian cùng xe với khách khác.}
\label{bang:c4-doi-uutien}
\small
\setlength{\tabcolsep}{3pt}
\begin{tabular}{@{}p{5.2cm}rrrr@{}}
\toprule
& \multicolumn{2}{c}{\textbf{Ưu tiên ổn định}} & \multicolumn{2}{c}{\textbf{Ưu tiên chi phí}}\\
\cmidrule(lr){2-3}\cmidrule(l){4-5}
\textbf{Chỉ số} & \textbf{Không trần} & \textbf{Trần 30} & \textbf{Không trần} & \textbf{Trần 30}\\
\midrule
Lượt phục vụ & 2.434 & 2.338 & 2.502 & 2.364\\
Muộn quá một phút & 203 & 76 & 379 & 72\\
Không muộn quá một phút & 2.231 & 2.262 & 2.123 & 2.292\\
Không muộn quá hai phút & 2.332 & 2.327 & 2.280 & 2.353\\
Không muộn quá năm phút & 2.416 & 2.338 & 2.466 & 2.364\\
Khách hoàn thành có đi chung & 555 & 313 & 1.027 & 432\\
Khách bị sửa giờ trả do điều phối & 174 & 33 & 434 & 106\\
Trong đó, bị sửa từ hai lần & 12 & 0 & 50 & 2\\
Đón muộn theo giới hạn dịch vụ & 51 & 43 & 39 & 44\\
Muộn quá một phút khi lời hứa có dự báo & 137 & 10 & 318 & 11\\
Tổng dời lớn nhất của một khách (s) & 590,1 & 29,2 & 709,1 & 29,7\\
\bottomrule
\end{tabular}
\end{table}
% EV: S15-01 (mục 2, 5a--5f); S15-02 (pooled); S15-04 (kiểm bảng và các chênh lệch từ TSV).

Khi cùng giữ trần 30 giây, ưu tiên chi phí phục vụ thêm 26 lượt và có thêm 119 khách hoàn thành có đi chung
so với ưu tiên ổn định (Bảng~\ref{bang:c4-doi-uutien}). Số lượt không muộn quá một phút tăng 30. Đổi lại, số khách
bị sửa giờ trả tăng từ 33 trong 2.338 lên 106 trong 2.364, tương ứng 1,41\% và 4,48\%; số khách bị sửa từ hai lần
tăng từ 0 lên 2. Số lượt đón muộn tăng từ 43 lên 44. Như vậy, cách ưu tiên ổn định vẫn cho ít khách bị sửa lời
hứa hơn, còn ưu tiên chi phí nhận và ghép được nhiều khách hơn trong thiết lập này.

Đúng giờ đến và ít sửa lời hứa là hai chỉ số khác nhau. Ưu tiên chi phí có trần có 72 lượt muộn quá một phút,
ít hơn 4 lượt so với ưu tiên ổn định có trần, nhưng nhiều khách bị sửa giờ hơn. Khi dùng lời hứa có dự báo, số
lượt muộn quá một phút là 11 và 10 trên hai nhóm khách tương ứng. Các chênh lệch nhỏ này không đủ để chọn một
cách làm tốt hơn về mọi mặt. Trung vị độ dài lời hứa đầu chưa dự báo là 809,642 giây với ưu tiên chi phí và
815,060 giây với ưu tiên ổn định, trên hai tập khách khác nhau.
% EV: S15-01 (5b--5e); S15-02. Độ dài dùng p0-t0; không quy thành chờ/đi vòng của từng khách.

Không trần cho thấy rõ cái giá của đổi thứ tự. Ưu tiên chi phí phục vụ thêm 68 lượt và có thêm 472 khách hoàn
thành có đi chung so với ưu tiên ổn định, nhưng có thêm 176 lượt muộn quá một phút và thêm 260 khách bị sửa giờ
trả. Trần vì vậy vẫn có vai trò khi bộ chọn chuyển sang ưu tiên chi phí. Trong thứ tự này, thêm trần 30 giây làm
giảm 138 lượt phục vụ và 595 khách hoàn thành có đi chung; đồng thời có ít hơn 307 lượt muộn quá một phút và
thêm 169 lượt được phục vụ, không muộn quá một phút.
% EV: S15-01 (mục 2, 5f--5g); S15-04. Các chênh lệch là tổng trên tập yêu cầu, không cùng tập khách phục vụ.

Phần giảm số lượt trễ phải đọc cùng phần không phục vụ. Với ưu tiên ổn định, 127 lượt trễ ít hơn đi cùng 96 lượt
phục vụ ít hơn và 31 lượt không muộn quá một phút nhiều hơn. Với ưu tiên chi phí, các số tương ứng là 307, 138
và 169. Chênh lệch giữa hai mức giảm trễ là 180, nhưng không có nghĩa 180 khách cụ thể được bảo vệ chỉ nhờ đổi
bộ chọn. Chênh lệch này cùng chiều ở 29 trong 34 kịch bản và cả năm ngày; các kịch bản phụ thuộc nhau nên đây
chỉ là mô tả trên dữ liệu đã dùng.
% EV: S15-01 (mục 2, 4); S15-02 (interactions); 180 = (138-96) + (169-31).

Ở ưu tiên chi phí có trần, tổng dời lớn nhất do điều phối là 29,657 giây và không khách nào vượt 30 giây.
Kết quả cho thấy cấu hình có trần giữ được giới hạn đã đặt khi thay thứ tự xếp hạng. Nó chưa chứng minh rằng
riêng trần đủ bảo đảm giờ đến thực tế, vì khóa, ràng buộc dịch vụ, bộ sinh và luật phục hồi vẫn cùng tham gia.

Mức trần thích hợp còn phụ thuộc tiêu chí dịch vụ. Với ưu tiên chi phí, trần 30 giây tăng số lượt được phục vụ
và không muộn quá một phút từ 2.123 lên 2.292; nếu chấp nhận muộn tới năm phút thì số lượt đáp ứng lại giảm
từ 2.466 xuống 2.364. Đây là hai cách đọc cùng kết quả đã có, không phải khảo sát mức khách chấp nhận chờ.
Trần 30 giây là điểm thực nghiệm, không được chọn làm ngưỡng tối ưu hay ngưỡng cảm nhận của hành khách.
% EV: S15-01 (5a); S15-02 (timely60, timely300); K36; K52.

\subsection{Lời hứa có dự báo và giới hạn của phép so}

Với ưu tiên chi phí có trần, tính lại lời hứa đầu theo dự báo cho 11 lượt muộn quá một phút thay vì 72,
trên cùng 2.364 hành trình; lời hứa dài thêm trung bình 7,30 giây. Với cấu hình không trần, các số lượt muộn
là 318 thay vì 379, và lời hứa dài thêm trung bình 7,02 giây. Giờ trả thực tế và việc xe chở ai không đổi
trong phép tính này. Dự báo điều chỉnh phần đường dự kiến chậm đi, còn luật C giới hạn phần dời do điều phối;
hai thành phần không thay thế nhau và không tạo thành bảo đảm vô điều kiện về giờ đến.
% EV: S15-01 (5b); F01--F04. 21 ô bật causal-no15; 13 ô còn lại giữ p0.

Các lượt ưu tiên chi phí dùng bản chạy riêng. Phép đối chiếu hành vi trên bốn cặp mẫu và việc tính lại các
chỉ số ưu tiên ổn định giúp kiểm tra khả năng so sánh, nhưng không chứng minh mọi khác biệt giữa hai bản
chạy đã được loại bỏ. Các con số là phép so thăm dò theo cấu hình trên một bộ mô phỏng và năm ngày đã xem,
không phải ước lượng tác động nhân quả riêng của một tiêu chí. Phép sàng lọc trên các phương án đã ghi của
tập tắc đường cố định được giữ ở Phụ lục~\ref{phuluc:D}; nó bổ trợ việc hiểu bộ chọn, không thay kết quả
của những lần mô phỏng đầy đủ ở đây.
% EV: STAGE1B-PLAN.md mục 1.1 và amendment mục 3; S15-01 (mục 0, 3); G0 chỉ behavioural-only.
'''
c4 = between(c4, start, end, new_section)
c4 = once(c4, 'Chương này dùng bốn tập kịch bản (Bảng~\\ref{bang:c4-taptu}).', 'Chương này dùng bốn tập kịch bản (Bảng~\\ref{bang:c4-taptu}). Trên tập kiểm tra chính còn có phép so hai\nthứ tự chọn phương án ở mục~\\ref{muc:c4-vaitro}; phép so này dùng lại cùng kịch bản, không bổ sung ngày dữ liệu.')
c4 = once(c4, '170 (năm cấu hình) và 34 (C-120)\\\\', '170 (năm cấu hình), 34 (C-120); thêm 68 ưu tiên chi phí\\\\')
c4 = once(c4, r'''cấu hình dùng cùng bộ sinh, bộ kiểm và bộ chọn với luật C; điều phối không trần chỉ khác luật C trần 30 giây ở trần giờ
trả.''', r'''cấu hình trong từng thứ tự ưu tiên dùng cùng bộ sinh, bộ kiểm và bộ chọn với luật C; điều phối không trần chỉ
khác luật C trần 30 giây ở trần giờ trả. Các mục trước mục~\ref{muc:c4-vaitro}, cùng các phép so dự báo và đệm
sau đó, dùng thứ tự ưu tiên ổn định nếu không ghi khác.''')
c4 = once(c4, 'trên, các đợt chạy khác chỉ được nhắc khi nêu phạm vi kiểm tra', 'trên và 68 lượt ưu tiên chi phí; các đợt chạy khác chỉ được nhắc khi nêu phạm vi kiểm tra')
c4 = once(c4, 'Trên 3.672 yêu cầu của tập kiểm tra chính, luật C với trần 30 giây', 'Với bộ chọn ưu tiên ổn định trên 3.672 yêu cầu của tập kiểm tra chính, luật C trần 30 giây')
c4 = once(c4, 'cấu hình). Số đếm gộp trên mọi kịch bản;', 'cấu hình, với bộ chọn ưu tiên ổn định). Số đếm gộp trên mọi kịch bản;')
c4 = once(c4, 'các quyết định sẽ khác; luật mỗi lần một trần chưa được cài đặt và chưa chạy.', r'''các quyết định sẽ khác; luật mỗi lần một trần chưa được cài đặt và chưa chạy.

Khi chuyển sang ưu tiên chi phí (mục~\ref{muc:c4-vaitro}), cấu hình có trần 30 giây có một bản ghi chỉ vượt
do cộng dồn: phần đã dùng 24,404 giây cộng lần dời 13,558 giây thành 37,962 giây. Yêu cầu mới liên quan cuối
cùng vẫn được phục vụ. Cấu hình này có hai khách bị sửa giờ trả từ hai lần. Các số 0 của bảng trên chỉ thuộc
phạm vi ưu tiên ổn định đã nêu, không áp cho mọi thứ tự chọn phương án.
% EV: S15-01 (5d--5e); S15-04. Một witness, không phải một yêu cầu cuối cùng bị từ chối.

Trên 2.502 lịch sử khách được phục vụ của cấu hình ưu tiên chi phí không trần, có 7 lịch sử mà mỗi lần dời
không quá 120 giây nhưng tổng độ lớn vượt 120 giây. Ví dụ minh họa, hai lần dời 70 và 60 giây đều dưới trần
từng lần 120 giây, nhưng tổng 130 giây vượt trần cộng dồn. Số 7 đếm lịch sử khách theo phép kiểm ở mức 120
giây, không đếm khách vi phạm trần 30 giây. Nhánh so trực tiếp với luật giới hạn từng lần không được thực
hiện vì phép sàng lọc chưa đạt mốc 10 lịch sử đã đặt để phân bổ nguồn lực. Mốc này không phải ngưỡng kiểm
định thống kê; việc dừng chưa chứng minh phép cộng dồn không có tác dụng.
% EV: S15-01 (H7); S15-02 (h7_served); STAGE1B-PLAN.md mục 6,9. Ví dụ 70+60 là dựng tay.
''')
c4 = once(c4, r'''\item Bộ chạy dựng lại từ mã nguồn có thể khác bản đã lưu, nên lần chạy mới phải cho đúng kết quả cũ trước khi so với
các số ở đây.''', r'''\item Bản chạy ưu tiên chi phí khác bản lưu của ưu tiên ổn định. Bốn cặp đối chiếu hành vi và việc tái lập các
chỉ số nền không chứng minh tương đương toàn bộ hai bản. Kết quả ở mục~\ref{muc:c4-vaitro} vì vậy vẫn là phép so
thăm dò theo cấu hình, không loại trừ mọi khác biệt thời gian chạy.''')
c4 = once(c4, r'''\item Bộ chọn ưu tiên chi phí chưa được chạy thành lần mô phỏng đầy đủ, và luật mỗi lần một trần chưa được cài đặt
(mục~\ref{muc:c4-congdon}). Chưa tách được phần của trần với phần của bộ chọn, và chưa biết phần cộng dồn có đổi kết quả
hay không.''', r'''\item Phép so hai thứ tự chọn phương án đã có kết quả ở trần 30 giây và không trần, nhưng chưa tạo thành
đường đánh đổi nhiều mức cho ưu tiên chi phí. Luật giới hạn từng lần chưa được cài đặt; các bản ghi chỉ vượt do
cộng dồn và bảy lịch sử sàng lọc không thay cho phép so hai luật trên diễn biến mô phỏng đầy đủ.''')
changes['08-chuong4.tex'] = c4

conclusion = r'''% Cập nhật ngày 15/10/2026; S15-01--S15-04 và các nguồn đã ghi trong bản trước.
\chapter{Kết luận}
\label{chuong:ketluan}

Khóa luận đã đặc tả, cài đặt và đánh giá trên mô phỏng một trần cho tổng phần dời giờ trả do điều phối của từng
hành khách. Phương pháp kết hợp giới hạn phần điều phối kiểm soát được, lời hứa có tính trước hồ sơ tắc đường
và sổ theo từng khách. Chương này tổng hợp các kết quả, những đánh đổi quan sát được và phần còn cần kiểm tra.

\section{Kết quả và đánh đổi}
\label{muc:c5-tongket}

Luật C cộng độ lớn của từng lần dời giờ trả so với mốc giữ nguyên kế hoạch đã được tính lại theo thời gian đi
hiện tại. Mốc này kế thừa công thức 7 của Hörl và Zwick~\cite{horl2022}. Dưới điều kiện giữ nguyên kế hoạch ở
mục~\ref{muc:giunguyen}, phương án giữ nguyên không tiêu thêm trần. Trên tập kiểm tra chính, không khách nào
vượt trần 30 giây ở cả hai thứ tự chọn phương án; tổng dời lớn nhất là 29,239 giây khi ưu tiên ổn định và
29,657 giây khi ưu tiên chi phí kế hoạch. Đây là giới hạn phần tính cho điều phối, không phải bảo đảm giờ đến
thực tế cách lời hứa đầu nhiều nhất 30 giây.
% EV: A01; A06; A13; A22; X3-27; J01; J03; S15-01 (5d); S15-02.

Với ưu tiên ổn định, thêm trần 30 giây làm số lượt phục vụ giảm từ 2.434 xuống 2.338 và số khách hoàn thành
có đi chung giảm từ 555 xuống 313. Đổi lại, số lượt được phục vụ và không muộn quá một phút tăng từ 2.231
lên 2.262 trên 3.672 yêu cầu. Với ưu tiên chi phí, các số phục vụ là 2.502 và 2.364, số khách hoàn thành có
đi chung là 1.027 và 432, còn số lượt không muộn quá một phút là 2.123 và 2.292. Trần vẫn đi cùng độ trễ
thấp hơn khi bỏ ưu tiên ổn định khỏi thứ tự xếp hạng, nhưng đánh đổi về phục vụ và ghép khách cũng lớn hơn.
% EV: S15-01 (mục 2, 5f); S15-02. Nhóm khách phục vụ thay đổi giữa các cấu hình.

Khi cùng giữ trần, ưu tiên chi phí phục vụ thêm 26 lượt và có thêm 119 khách hoàn thành có đi chung so với
ưu tiên ổn định. Cái giá nằm ở nhiều khách bị sửa lời hứa hơn: 106 thay vì 33; số khách bị sửa từ hai lần
là 2 thay vì 0. Số lượt đón muộn là 44 thay vì 43. Vì thế, kết quả không thay thế bộ chọn ưu tiên ổn định
bằng một bộ chọn tốt hơn ở mọi tiêu chí. Hai thứ tự cho hai cách sử dụng phần linh hoạt còn lại trong cùng
trần; lựa chọn cần dựa trên mục tiêu dịch vụ và dữ liệu về mức chấp nhận của khách.
% EV: S15-01 (5d--5f); S15-04.

Các đối chứng cho thấy lợi ích có giới hạn. Dưới ưu tiên ổn định, luật hạn chót 30 giây cũng phục vụ 2.338
lượt; khi so cùng ngưỡng muộn hơn lời hứa đầu quá 30 giây, luật C có 292 và luật hạn chót có 289 lượt muộn.
Khi có dự báo, điều phối không trần đệm thêm 30 giây vào mọi lời hứa cũng đạt 2.328 lượt không muộn quá một
phút, bằng luật C, và hơn ở hai phút: 2.366 so với 2.338. Đổi lại, phần lời hứa dài thêm trung bình là 37,2
thay vì 7,4 giây. Những kết quả này không cho phép chọn luật C chỉ từ một số đo đúng giờ.
% EV: C01; C4A-01; N-21; C10; N-16; N-18; K05; K51.

Trần 120 giây dưới ưu tiên ổn định phục vụ 2.387 lượt, có 2.255 lượt không muộn quá một phút và 2.366 lượt
không muộn quá hai phút trên 3.672 yêu cầu. Đây là một điểm thêm, chưa xác định trần tối ưu. Phép so trực
tiếp luật cộng dồn với luật giới hạn từng lần vẫn chưa có. Bảy lịch sử thỏa điều kiện sàng lọc ở mức 120 giây
trong nhánh ưu tiên chi phí không trần, cũng như một bản ghi chỉ vượt do cộng dồn trong nhánh có trần 30
giây, cho thấy các điều kiện khác nhau của hai luật nhưng chưa cho biết chênh lệch kết quả phục vụ.
% EV: N-06; L1a-02; S15-01 (5d--5e, H7); S15-02; K08.

\section{Vai trò của dự báo và phần còn lại}

Trên các hành trình ưu tiên chi phí có trần đã ghi, tính lại lời hứa có dự báo cho 11 lượt muộn quá một phút
thay vì 72, trong 2.364 lượt phục vụ; lời hứa dài thêm trung bình 7,30 giây. Đối với ưu tiên ổn định có trần,
trên 21 kịch bản ngày thường có dự báo, số lượt muộn giảm từ 68 xuống 2 trong 1.471 lượt phục vụ. Đây là
hai phạm vi khác nhau; dự báo chỉ đổi giờ hứa trong phép tính lại, không đổi việc xe chở ai hay giờ trả thực tế.
% EV: S15-01 (5b); F17; F04; F03; N4B8-01.

Sổ theo từng khách cho biết dự báo giải thích được phần nào của độ lệch. Trong 6.217 giây trễ của 68 khách
ngày thường dưới ưu tiên ổn định, có 82 giây tính cho điều phối, 5.353 giây được hồ sơ lịch sử giải thích và
783 giây còn lại. Chiều tuyết 15/11/2018 cho thấy giới hạn: số khách trễ quá một phút chỉ giảm từ 41 xuống
34 trong 601 khách được phục vụ. Trong 3.445,9 giây trễ của 41 khách đó, 8,6 giây tính cho điều phối, 251,5
giây được hồ sơ lịch sử giải thích và 3.185,9 giây còn lại. Các phần được làm tròn riêng. Phần còn lại không
đồng nghĩa với phần không thể biết trước; sổ là phép phân rã theo mô hình, không xác định lỗi của từng bên.
% EV: F09; N4B8-11; F13; B20; N4B-05; K04; K19; X3-12.

\section{Hạn chế}
\label{muc:c5-hanche}

Các kết quả có tính thăm dò trên 34 kịch bản phụ thuộc nhau thuộc năm ngày đã xem. Mô phỏng có tải thấp,
thời gian dừng đón/trả bằng 0 trong các mẫu đã kiểm và một hệ số tắc đường chung cho toàn thành phố.
Các lượt ưu tiên chi phí dùng bản chạy khác các lượt ưu tiên ổn định; kiểm hành vi trên bốn cặp mẫu chưa
chứng minh tương đương toàn bộ. Chưa có căn cứ suy rộng các chênh lệch thành tác động nhân quả riêng của
bộ chọn, lợi nhuận thực tế, hiệu quả ở tải cao hoặc sự hài lòng của hành khách.

Phần dự báo dùng trung bình lịch sử theo khung 15 phút, lấy điều kiện hiện tại làm mốc; mỗi ngày bật dự báo
chỉ có hai hoặc ba ngày nguồn. Phép kiểm giờ đón vẫn dùng giờ chưa dự báo, trong khi 34 trong 1.471 khách
ngày thường được hiển thị giờ đón đầu vượt hạn đón. Phép thử đưa dự báo trực tiếp vào điều phối có ba lượt
dừng vì kế hoạch không khả thi. Khả năng dự báo và tính nhất quán của phần tích hợp cần được đánh giá thêm.
% EV: I01--I07; B06; B11; B12; S15-01; STAGE1B-PLAN mục 1.1,3; F01--F07; F12.

Khóa luận kế thừa các thành phần đã có: đo và kiểm soát thay đổi lời hứa của Fielbaum và
Alonso-Mora~\cite{fielbaum2020}, mốc tuyến cũ tính lại của Hörl và Zwick~\cite{horl2022}, cùng hạn chót từ lời
hứa đầu của Schulz và Pfeiffer~\cite{schulz2026}. Đối chứng chỉ mượn thành phần hạn chót, chưa tái hiện toàn
bộ các hệ thống đó. Phần thực hiện ở đây là đặc tả, cài đặt, kiểm giới hạn cộng dồn trên phần dời tính cho
điều phối của từng khách, kết hợp lời hứa theo dự báo và sổ theo từng khách, rồi đo các đánh đổi trong phạm vi mô phỏng.

\section{Hướng phát triển}
\label{muc:c5-huongphattrien}

Thứ nhất, cần khảo sát thêm các mức trần dưới cùng một thứ tự chọn phương án, trên dữ liệu phát triển mới.
Trần 30 giây trong khóa luận giới hạn phần dời do điều phối sau khi nhận khách; nó không phải giới hạn toàn bộ
thời gian chờ đón hay đi vòng. Muốn chọn mức triển khai cần biết khách chấp nhận bao nhiêu thay đổi và hãng xe
đánh đổi phục vụ với ổn định thế nào. Không điều chỉnh lại kết quả đã có để chọn một mức có lợi.

Thứ hai, cần mở rộng lịch sử giao thông cùng thành phố từ dữ liệu chuyến taxi theo tháng của
TLC~\cite{tlc}, kiểm chất lượng và độ phủ theo vùng, ngày, giờ trước khi dùng. Phải tách thời gian học, chọn
cấu hình và kiểm tra trên ngày chưa xem; mô hình phức tạp hơn chỉ được giữ nếu cải thiện ngoài tập học.
Ngày có sự kiện cần được đánh giá riêng, chỉ dùng tín hiệu thực sự có ở thời điểm hứa. Dữ liệu mô phỏng
bổ sung hữu ích cho thử tình huống khó, nhưng không thay bằng chứng dự báo trên giao thông thực.

Thứ ba, cần sửa phép kiểm giờ đón để phù hợp với lời hứa theo dự báo, đồng thời kiểm dự báo sai, dữ liệu
đến chậm và trường hợp thiếu lịch sử. Việc sửa cần có phép thử mô phỏng đầy đủ ở cấu hình mới; kết quả
tính lại lời hứa trên hành trình cũ không xác nhận thay đổi trong quyết định nhận và ghép khách.

Thứ tư, phép so luật cộng dồn với luật giới hạn từng lần vẫn là hướng kiểm tra riêng, cần đủ tình huống
phân biệt hai luật và kế hoạch phù hợp. Cũng cần thử hạn chót nới theo mốc giữ nguyên kế hoạch, mở rộng tải,
tắc đường riêng trên từng đoạn và mô hình khách hủy chuyến. Các hướng này giữ nguyên kết quả và giới hạn
của những phép thử đã trình bày, thay vì coi các phần chưa đo là ưu thế đã có.
'''
load('09-chuong5.tex')
changes['09-chuong5.tex'] = conclusion

app = load('11-phuluc.tex')
app = once(app, r'''Tập cố định dùng bộ chạy cũ không có luật phục hồi; các tập còn lại dùng''', r'''Tập cố định dùng bộ chạy cũ không có luật phục hồi; các lượt ưu tiên ổn định của các tập còn lại dùng''')
app += r'''

\section{So sánh thứ tự chọn phương án và chứng cứ bổ sung}
\label{phuluc:D}
\setcounter{table}{0}

Phép so ở mục~\ref{muc:c4-vaitro} bổ sung 68 lượt ưu tiên chi phí trên cùng tập kiểm tra chính, không bổ sung
ngày dữ liệu. Bản chạy được dựng từ phiên bản \texttt{dce84ec}; các lượt ưu tiên ổn định dùng bản đã lưu.
Bốn cặp kiểm hành vi cho kết quả khớp trên các mẫu được thử, nhưng không chứng minh tương đương toàn bộ hai
bản. 68 lượt chính hoàn tất và qua kiểm tính toàn vẹn cuối. Các phép kiểm này xác nhận dữ liệu hợp lệ cho
phân tích trong phạm vi đã nêu, không xác nhận hiệu quả ngoài đời.
% EV: S15-01 (mục 0); main-final-admission.json; RUNNER-PROVENANCE.md; G0 behavioural-only.

Bản trích xuất gồm 9.638 dòng khách của 136 lượt, tương ứng bốn cấu hình. Chương trình kiểm lại bằng mã riêng
đạt 83 phép đối chiếu số học từ bản trích xuất, gồm mẫu số, các số gộp và đồng nhất thức giữa phục vụ, trễ và
không trễ. Đây không phải một bộ giải mã bản ghi thô và dự báo độc lập thứ hai. Bảng số liệu, bản trích xuất
và kết quả kiểm được lưu kèm hồ sơ; mã nguồn và các đầu ra có mã băm để truy vết.
% EV: S15-01; S15-02; S15-04; results/stage1b-results.sha256.

Kiểm thử trên phiên bản ưu tiên chi phí có 1.033 trong 1.034 bài đạt. Bài còn lại đòi cây mã nguồn có thay
đổi chưa ghi nhận, nên thất bại khi chạy trên cây sạch; không được báo cả bộ kiểm thử đạt. Đây là kết quả
của phiên bản riêng, không thay kết quả 1.025 bài của phiên bản đã lưu ở Phụ lục~\ref{phuluc:C}.
% EV: evidence/dotnet-test-costfirst-2026-10-15.log; RESUME-STATUS.md; không sửa C# trong lượt cập nhật luận văn.

Phép sàng lọc dưới đây được giữ để phân biệt chọn lại trên dữ liệu đã ghi với chạy cả diễn biến mới.
'''
app += screen
app += r'''

Phép sàng lọc giữ nguyên các lượt sau lựa chọn đang xét, nên không cho biết số lượt phục vụ và đúng giờ
khi cả lần mô phỏng đi theo lựa chọn khác. Kết quả mô phỏng đầy đủ của hai thứ tự được báo riêng ở
Bảng~\ref{bang:c4-doi-uutien}; không cộng các chênh lệch sàng lọc thành lợi ích vận hành.
'''
changes['11-phuluc.tex'] = app

for name, content in changes.items():
    assert '\\section{Hướng phát triển}' in content or name != '09-chuong5.tex'
    (ROOT/'noi-dung'/name).write_text(content, encoding='utf-8', newline='\n')

manifest = {'scope': 'Thesis update only; old v4 preserved; no sealed experiment inputs/outputs changed.', 'changed': []}
for name in changes:
    old, new = (BASE/'noi-dung'/name).read_bytes(), (ROOT/'noi-dung'/name).read_bytes()
    manifest['changed'].append({'file':name,'oldSha256':hashlib.sha256(old).hexdigest(),'newSha256':hashlib.sha256(new).hexdigest()})
with (ROOT/'source-update.json').open('x', encoding='utf-8') as f:
    json.dump(manifest, f, ensure_ascii=False, indent=2)
print(json.dumps(manifest, ensure_ascii=False, indent=2))
