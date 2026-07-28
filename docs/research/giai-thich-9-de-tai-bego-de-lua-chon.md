# Giải thích 9 đề tài nghiên cứu mở rộng từ BeGo/OptiGo

Ngày biên soạn: 2026-07-16
Mục đích: giúp lựa chọn đề tài bằng ngôn ngữ dễ hiểu, nhưng vẫn giữ đúng bản chất học thuật và khả năng kiểm chứng bằng dữ liệu công khai.

## 1. Vì sao tài liệu này tồn tại?

BeGo/OptiGo hiện giải quyết một bài toán khá đặc trưng: một nhóm người muốn đi chơi hoặc gặp nhau, hệ thống cần chọn nơi đến, tìm điểm đón chung, phân người lên xe và tính lộ trình sao cho tổng chi phí hợp lý, đồng thời không để một người chịu thiệt quá lớn.

Khi mở rộng thành đề tài nghiên cứu lớn, chỉ xây được một ứng dụng chạy tốt là chưa đủ. Một đề tài học thuật cần trả lời được bốn câu hỏi:

1. Vấn đề nghiên cứu cụ thể là gì và vì sao nó chưa được giải quyết thỏa đáng?
2. Phương pháp đề xuất khác gì so với cách đã có?
3. Dùng dữ liệu nào để kiểm tra mà người khác cũng có thể tải và chạy lại?
4. Kết quả nào sẽ chứng minh phương pháp tốt hơn, và kết quả nào sẽ khiến ta phải thừa nhận rằng giả thuyết không đúng?

Chín đề tài dưới đây đã được chọn theo nguyên tắc **data-first**, nghĩa là kiểm tra dữ liệu trước khi chốt ý tưởng. Nếu dữ liệu công khai không đủ để chứng minh luận điểm trung tâm thì đề tài bị loại, dù ý tưởng nghe có vẻ hấp dẫn.

## 2. Một số thuật ngữ dùng xuyên suốt

### 2.1. Dữ liệu thật và mô phỏng

- **Dữ liệu thật** là các chuyến đi, tọa độ, thời gian hoặc nhãn đã được quan sát trong thực tế. Ví dụ: một chuyến taxi thực sự đón khách lúc 8:05 tại vùng A và trả khách lúc 8:28 tại vùng B.
- **Mô phỏng** là cho nhiều thuật toán cùng xử lý lại những yêu cầu đó trong một môi trường kiểm soát. Thuật toán có thể quyết định khác với quyết định lịch sử, nhưng đầu vào phải giống nhau.
- Mô phỏng trên yêu cầu thật vẫn có giá trị học thuật đối với bài toán tối ưu. Điều không được làm là tự sinh dữ liệu theo cách có lợi cho thuật toán của mình rồi dùng chính dữ liệu ấy để tuyên bố hơn hẳn phương pháp khác.

### 2.2. Baseline

**Baseline** là phương pháp đối chứng. Có thể hiểu là “đối thủ chuẩn” mà phương pháp mới phải so sánh. Baseline có thể là cách đơn giản như đón tận cửa, hoặc thuật toán đã được công bố như OR-Tools, ALNS, Transformer. Một đề tài không có baseline mạnh rất khó thuyết phục.

### 2.3. Replay theo thời gian

**Replay** là phát lại các yêu cầu theo đúng thứ tự thời gian đã quan sát. Nếu dữ liệu ghi nhận một yêu cầu xuất hiện lúc 8:00:05 thì bộ mô phỏng chỉ cho thuật toán biết yêu cầu đó tại thời điểm tương ứng, không cho nhìn trước tương lai. Cách này gần với vận hành thật hơn việc đưa toàn bộ yêu cầu của ngày cho thuật toán ngay từ đầu.

### 2.4. Các chỉ số thường gặp

- **Trung bình** cho biết kết quả chung, nhưng có thể che giấu một số trường hợp rất tệ.
- **p95** là ngưỡng mà 95% trường hợp không vượt quá. Ví dụ p95 thời gian chờ bằng 12 phút nghĩa là 95% hành khách chờ không quá 12 phút. Nó phản ánh phần “đuôi xấu” tốt hơn trung bình.
- **Gini** đo độ chênh lệch giữa các thành viên, bằng 0 khi mọi người gần như ngang nhau. Không nên dùng Gini một mình vì mọi người cùng chịu chi phí rất cao vẫn có thể cho Gini thấp.
- **Regret – độ hối tiếc/thiệt so với lựa chọn tốt hơn** là phần chi phí tăng thêm vì thuật toán chọn một phương án thay vì phương án tốt nhất làm mốc.
- **Không kém hơn** là phép kiểm tra xem phương pháp mới có giữ được một mặt quan trọng hay không. Ví dụ giảm quãng đường xe nhưng tỷ lệ phục vụ không được giảm quá 0,5 điểm phần trăm.

### 2.5. Pareto

Một phương án là **không bị trội** nếu không có phương án khác vừa rẻ hơn, vừa ít đi bộ hơn, vừa giảm gánh nặng lớn nhất. Tập hợp các phương án như vậy tạo thành **biên Pareto**. Nó phù hợp khi không tồn tại một đáp án tốt nhất tuyệt đối mà chỉ có các đánh đổi khác nhau.

### 2.6. Ablation

**Ablation – thí nghiệm tháo từng thành phần** nghĩa là lần lượt bỏ một phần của phương pháp để xem phần đó có thật sự tạo ra cải thiện hay không. Nếu bỏ thành phần “ước lượng mật độ” mà kết quả không thay đổi, ta không thể khẳng định phần đó là đóng góp quan trọng.

### 2.7. Khoảng tối ưu

Trong bài toán tối ưu, **optimality gap – khoảng cách tới tối ưu** là mức chênh giữa phương án tốt nhất đang tìm được và cận lý thuyết của lời giải tối ưu. Khoảng bằng 0 chứng minh đã tìm được tối ưu; khoảng nhỏ cho biết lời giải hiện tại gần tối ưu đến đâu.

---

## 3. T1 – BeGo-CAST: điểm đón chung thích ứng theo bối cảnh

### 3.1. Hiểu đề tài bằng một tình huống đơn giản

Giả sử bốn người cần được đón trong cùng một khu phố. Đón từng người tận cửa làm xe phải rẽ nhiều lần. Bắt cả bốn đi bộ 500 m đến một điểm duy nhất lại không hợp lý vì có người ở gần, có người đi bộ khó khăn.

BeGo-CAST sẽ tự quyết định:

- nên dùng một hay nhiều điểm đón chung;
- mỗi người đi đến điểm nào;
- giới hạn đi bộ của từng người là bao nhiêu;
- trong khu vực đông yêu cầu có nên gom mạnh hơn khu vực thưa hay không;
- phân người cho xe và sắp thứ tự ghé các điểm như thế nào.

### 3.2. Bối cảnh và động lực nghiên cứu

Nhiều hệ thống đi chung xe dùng hai cực đơn giản: đón tận cửa hoặc cho phép đi bộ trong một bán kính cố định, chẳng hạn 300 m. Bán kính cố định không phản ánh mật độ yêu cầu, cấu trúc đường, số xe đang rảnh hay khả năng đi bộ khác nhau.

Điểm đón chung có thể giảm số lần dừng và quãng đường vòng, nhưng lợi ích vận hành thường được mua bằng việc chuyển một phần gánh nặng sang hành khách. Vì vậy câu hỏi học thuật không phải chỉ là “điểm đón chung có giảm đường xe không?”, mà là:

> Có thể tự điều chỉnh mức gom điểm đón theo từng tình huống để giảm chi phí xe mà vẫn giữ giới hạn phục vụ cho từng người hay không?

### 3.3. Đây là ngách nào? Có quá cũ không?

Ngách của đề tài là giao giữa:

- điều phối xe dùng chung theo thời gian thực;
- lựa chọn điểm dừng linh hoạt;
- tối ưu nhiều mục tiêu có ràng buộc cho từng hành khách.

Bài toán đi chung xe đã tồn tại lâu, nhưng **điểm dừng thích ứng theo mật độ và giới hạn cá nhân, được kiểm tra trên benchmark Manhattan mới năm 2026**, không phải một đề tài cũ bị làm lại. Phần dễ bị cũ là chỉ đề xuất “cho phép đi bộ tới điểm đón” vì nhiều paper đã làm. Phần mới phải nằm ở cơ chế thích ứng, ràng buộc phần đuôi xấu và đánh giá Pareto trên yêu cầu thật.

### 3.4. Đề tài dựa trên nghiên cứu nào?

Các nền tảng trực tiếp gồm:

- [Advancing Dynamic Ride-Pooling Simulation – A Highly Scalable Dispatcher, 2026](https://arxiv.org/pdf/2605.11798), cho thấy vai trò của mật độ yêu cầu, quy mô đội xe, điểm đi bộ và khả năng điều phối ở quy mô lớn.
- Dòng nghiên cứu **dynamic stop pooling**, tức gom các điểm đón/trả động thay vì luôn đón tận cửa.
- Hơn 20 paper DARP và DARP có điểm gặp trong tập 170 paper đã đọc, dùng cho mô hình ràng buộc, thuật toán chính xác và heuristic.
- [Manhattan Dial-a-Ride Benchmark 2026](https://zenodo.org/records/20452171), cung cấp đầu vào thực nghiệm hoàn chỉnh hơn nhiều bộ dữ liệu trước đó.

### 3.5. Nhu cầu thực tế và vấn đề được giải quyết

- Giảm xe chạy vòng và số lần dừng trong khu vực đông đúc.
- Tăng số người có thể phục vụ với cùng số xe.
- Tránh quy tắc đi bộ “một cỡ cho tất cả”.
- Cho người vận hành nhìn thấy rõ đánh đổi: giảm bao nhiêu quãng đường thì hành khách phải đi bộ/chờ thêm bao nhiêu.

### 3.6. Mục tiêu nghiên cứu

Xây dựng thuật toán chọn điểm đón, phân người–xe và xếp tuyến đồng thời, sao cho:

1. phục vụ được càng nhiều yêu cầu càng tốt;
2. không vi phạm giới hạn đi bộ, chờ và đi vòng của từng người;
3. giảm thời gian hoặc quãng đường vận hành của xe;
4. kiểm soát người chịu gánh nặng lớn nhất thay vì chỉ tối ưu trung bình.

### 3.7. Đóng góp học thuật dự kiến

- Mô hình điểm đón chung có giới hạn cá nhân và thích ứng theo mật độ.
- Cách chọn phương án theo thứ tự ưu tiên/ràng buộc thay vì trộn mọi thứ vào một điểm số khó giải thích.
- Thuật toán chính xác cho trường hợp nhỏ và thuật toán gần tối ưu cho trường hợp lớn.
- Bộ quy trình benchmark giữ nguyên đầy đủ thời gian, sức chứa và quan hệ đón–trả.
- Phân tích khi nào shared stop thật sự có lợi và khi nào nên quay về đón tận cửa.

### 3.8. Cơ sở lý thuyết

- **DARP – bài toán xe đưa đón theo yêu cầu:** xe phải đón và trả nhiều hành khách, tuân theo sức chứa và thời gian.
- **Set cover – bài toán phủ tập:** chọn ít điểm đón nhưng vẫn cho mọi hành khách ít nhất một điểm có thể đi tới.
- **Vehicle routing – định tuyến xe:** quyết định xe nào đi qua những điểm nào và theo thứ tự nào.
- **Tối ưu nhiều mục tiêu/Pareto:** xem xét đồng thời chi phí xe, đi bộ và gánh nặng lớn nhất.
- **ALNS – tìm kiếm lân cận lớn thích ứng:** liên tục phá một phần lời giải rồi sửa lại theo nhiều chiến lược; thích hợp với bài toán lớn khó giải chính xác.

### 3.9. Chứng minh bằng dữ liệu nào và bằng cách nào?

Dataset chính là [Manhattan DARP 2026](https://zenodo.org/records/20452171): 24 ngày taxi thật, điểm dừng ảo từ mạng đường OSM, ma trận thời gian đi lại, các cửa sổ 2/4/16 giờ và nhiều quy mô đội xe.

Chia 24 ngày theo thời gian:

- 14 ngày đầu để xây dựng/hiệu chỉnh;
- 5 ngày tiếp theo để chọn cấu hình;
- 5 ngày cuối khóa lại để kiểm tra.

So sánh với:

- đón tận cửa;
- điểm gần nhất;
- bán kính đi bộ cố định 150/300/500 m;
- shared stop tĩnh;
- OptiGo hiện tại;
- OR-Tools hoặc mô hình chính xác trên instance nhỏ.

Chứng minh tốt hơn nếu giảm đáng kể thời gian/quãng đường xe trên các ngày chưa từng dùng để tinh chỉnh, trong khi tỷ lệ phục vụ và p95 chờ/đi bộ/đi vòng không bị xấu đi quá giới hạn đã công bố trước.

### 3.10. Điều có thể và không thể tuyên bố

Có thể nói: “thuật toán giảm chi phí vận hành dưới các giới hạn đi bộ xác định”. Không thể nói: “hành khách thật thích đi bộ tới các điểm đó”, vì dataset taxi không chứa mức sẵn lòng đi bộ. Muốn khẳng định về sự chấp nhận của người dùng cần khảo sát hoặc thử nghiệm thực địa riêng.

### 3.11. Đánh giá lựa chọn

Đây là đề tài cân bằng tốt nhất giữa tính mới, dữ liệu, khả năng chứng minh và mức tái sử dụng BeGo. Rủi ro kỹ thuật ở mức vừa phải và phạm vi có thể thu gọn rõ ràng.

---

## 4. T2 – BeGo-WAIT: quyết định khi nào ghép xe

### 4.1. Hiểu đơn giản

Khi một yêu cầu vừa xuất hiện, hệ thống có hai lựa chọn:

- ghép ngay với xe đang có;
- chờ thêm vài giây để có thể xuất hiện yêu cầu khác cùng hướng.

Ghép ngay làm khách chờ ít nhưng có thể bỏ lỡ cơ hội đi chung. Chờ lâu giúp gom được nhiều người nhưng làm trải nghiệm xấu. BeGo-WAIT học quy tắc “đợi hay ghép ngay” theo trạng thái hiện tại.

### 4.2. Bối cảnh và động lực

Nhiều hệ thống dùng chu kỳ cố định, ví dụ cứ 15 giây chạy thuật toán ghép một lần. Tuy nhiên 15 giây có thể quá dài ở giờ vắng và quá ngắn ở giờ cao điểm. Một ngưỡng số người chờ cũng chưa đủ vì mười yêu cầu đi mười hướng khác nhau không có giá trị ghép như mười yêu cầu cùng hướng.

### 4.3. Ngách và độ mới

Quyết định thời điểm ghép không hoàn toàn mới. Điểm mới nằm ở ba chỗ:

1. phát lại timestamp của yêu cầu thật thay vì sinh yêu cầu bằng phân phối xác suất;
2. giữ cùng một thuật toán ghép cho tất cả phương pháp, chỉ thay đổi quyết định về thời điểm;
3. có “lá chắn an toàn” buộc ghép trước khi người chờ quá giới hạn.

Đây là ngách hẹp hơn T1, dễ tạo một câu hỏi nghiên cứu sắc nét.

### 4.4. Nghiên cứu nền

Paper chính là [A Timely Match for Ride-Hailing and Ride-Pooling Services Using a Deep Reinforcement Learning Approach, Transportation Research Part C 2026](https://www.fransoliehoek.net/docs/Bao26TRC.pdf). Paper này rất mới và chỉ ra rằng thời điểm matching ảnh hưởng lớn đến chờ và đi vòng. Tuy vậy, paper dùng NYC TLC để hiệu chỉnh rồi sinh episode bằng Poisson, giả định xe chạy 40 km/h và tối đa hai đơn mỗi xe.

Đề tài mới giữ ý tưởng hay của paper nhưng thay phần đánh giá bằng replay trực tiếp trên Manhattan DARP.

### 4.5. Nhu cầu được giải quyết

- Giảm thời gian chờ không cần thiết ở giờ thưa.
- Tăng khả năng ghép ở giờ đông.
- Thích ứng với mức phân tán không gian, không chỉ số lượng request.
- Tạo chính sách đơn giản đủ nhanh để chạy online.

### 4.6. Mục tiêu và đóng góp

- Xây dựng chính sách chọn `ghép ngay` hoặc `chờ thêm Δ giây`.
- Tách ảnh hưởng của “thời điểm ghép” khỏi chất lượng thuật toán lập tuyến.
- Đề xuất trạng thái mô tả được cơ hội ghép: số người chờ, tuổi yêu cầu lâu nhất, mức cùng hướng, xe rảnh và mật độ không gian.
- Chứng minh chính sách không vi phạm giới hạn chờ.
- Nếu mô hình phức tạp tốt hơn, rút gọn thành cây quyết định để giải thích được.

### 4.7. Cơ sở lý thuyết

- **Xử lý theo lô:** gom nhiều yêu cầu rồi xử lý cùng lúc.
- **Contextual bandit – bài toán chọn hành động theo bối cảnh:** học hành động nào tốt trong một trạng thái mà không cần mô hình hóa tương lai quá dài.
- **Offline reinforcement learning – học tăng cường ngoại tuyến:** học từ dữ liệu/simulator có sẵn thay vì thử hành động nguy hiểm trên người dùng thật.
- **Safety constraint – ràng buộc an toàn:** dù mô hình đề xuất đợi, hệ thống vẫn buộc ghép khi thời gian chờ đạt trần.

### 4.8. Dataset và cách chứng minh

Dùng cùng 24 ngày [Manhattan DARP](https://zenodo.org/records/20452171), phát lại đúng thời điểm thực. Không tự sinh thêm request cho thí nghiệm chính.

Baseline:

- ghép ngay;
- chu kỳ cố định 5/10/15/20/30/60 giây;
- ghép khi hàng đợi đạt ngưỡng;
- ghép khi yêu cầu lâu nhất đạt ngưỡng;
- rolling insertion – chèn yêu cầu ngay vào tuyến đang chạy.

Đánh giá tỷ lệ phục vụ, tỷ lệ ghép chung, chờ trung bình/p95, đi vòng, thời gian xe, occupancy và vi phạm giới hạn. Phương pháp mới phải tốt trên phần lớn ngày test, không được chỉ thắng nhờ một giờ cao điểm đặc biệt.

### 4.9. Giới hạn tuyên bố

Dataset không ghi đầy đủ người đã hủy vì chờ, nên không được khẳng định dự đoán chính xác hành vi hủy. Giới hạn chờ là quy tắc dịch vụ do nghiên cứu đặt ra.

### 4.10. Đánh giá lựa chọn

Đề tài gọn, rõ, phù hợp nếu muốn thiên về online decision hoặc reinforcement learning nhưng không muốn xây toàn bộ hệ thống mới. Tính mới phụ thuộc mạnh vào real-trace replay và đánh giá an toàn; nếu chỉ dùng PPO để thay chu kỳ cố định thì khá dễ bị xem là cũ.

---

## 5. T3 – BeGo-XFER: đi chung xe có một lần chuyển xe

### 5.1. Hiểu đơn giản

Thay vì một xe chở hành khách từ đầu đến cuối, xe A có thể đưa người đó tới một điểm gặp, sau đó xe B đang đi cùng hướng sẽ chở tiếp. Đây giống đổi tuyến bus, nhưng được lập kế hoạch động cho xe dùng chung.

Không phải trường hợp nào đổi xe cũng có lợi. Ở giờ vắng, đổi xe chỉ làm mất thời gian. Ở giờ đông, nó có thể giúp xe bớt đi vòng và tăng số ghế được sử dụng.

### 5.2. Bối cảnh và ngách

Transfer trong ride-pooling đã được nghiên cứu, nhưng paper mới nhất vẫn chủ yếu tập trung vào tốc độ thuật toán. Hai câu hỏi còn mở là:

- khi nào mật độ đủ cao để transfer có lợi;
- điểm chuyển có an toàn, dễ tiếp cận và phù hợp cho người dùng hay không.

Ngách đề tài là **chỉ cho transfer khi có đủ bằng chứng về lợi ích và chỉ tại điểm đạt điều kiện accessibility**.

### 5.3. Có cũ không? Dựa trên paper nào?

Nền tảng trực tiếp là [Exact and Heuristic Dynamic Taxi Sharing with Transfers Using Shortest-Path Speedup Techniques, ATMOS 2025](https://drops.dagstuhl.de/storage/01oasics/oasics-vol137-atmos2025/OASIcs.ATMOS.2025.15/OASIcs.ATMOS.2025.15.pdf).

Paper báo cáo ở bộ Berlin mật độ cao:

- occupancy tăng khoảng 5,9%;
- thời gian vận hành xe giảm 4,6%;
- nhưng thời gian chuyến của hành khách tăng 3,1%.

Tác giả nêu rõ nghiên cứu tương lai cần xem xét an toàn, accessibility và thời gian giao thông thay đổi. Vì vậy đề tài không cũ nếu bám đúng khoảng trống này và chuyển đánh giá sang yêu cầu taxi thật.

### 5.4. Nhu cầu và vấn đề giải quyết

- Tăng khả năng kết nối các tuyến xe mà không bắt một xe đi vòng quá xa.
- Xác định ngưỡng mật độ để biết khu vực/khung giờ nào đáng triển khai.
- Tránh điểm chuyển không có curb ramp hoặc dữ liệu accessibility quá thiếu.
- Ngăn hệ thống lạm dụng transfer chỉ vì giảm chi phí nhà vận hành.

### 5.5. Mục tiêu và đóng góp

- Thuật toán cho phép tối đa một lần chuyển xe.
- Bộ phân loại/gate quyết định có mở transfer hay không.
- Cách chọn điểm chuyển có ràng buộc thời gian và accessibility.
- Phân tích theo mật độ để tìm điều kiện cần cho lợi ích.
- Kết quả âm vẫn có giá trị: có thể chứng minh transfer chỉ hữu ích trên một dải mật độ hẹp.

### 5.6. Cơ sở lý thuyết

- DARP động với một transfer.
- **Detour ellipse – miền điểm có thể ghé mà không đi vòng quá mức:** dùng để thu hẹp điểm chuyển ứng viên.
- Thuật toán đường đi ngắn có tăng tốc như contraction hierarchy.
- Mô hình phân loại có độ tin cậy để mở/đóng transfer.
- Tối ưu có ràng buộc accessibility.

### 5.7. Dataset và chứng minh

- [Manhattan DARP 2026](https://zenodo.org/records/20452171) cho yêu cầu và mạng dừng.
- [RampNet](https://huggingface.co/datasets/projectsidewalk/rampnet-dataset) và Project Sidewalk cho bằng chứng curb-ramp tại phần khu vực có dữ liệu.
- OSM cho cấu trúc đường.

Baseline gồm không transfer, transfer tại hub gần nhất, transfer tại điểm trung tâm, thuật toán KaRRiT exact/heuristic và transfer ở mọi nơi.

Kết quả phải được chia theo mật độ. Chỉ so trung bình toàn thành phố có thể che giấu việc transfer tốt ở giờ đông nhưng hại ở giờ vắng. Ngoài thời gian xe, phải đo thời gian transfer, handoff thất bại, thời gian hành khách và độ phủ accessibility.

### 5.8. Giới hạn và rủi ro

Đây là đề tài khó nhất trong chín đề tài. Phạm vi cần khóa ở một transfer, ma trận thời gian cố định và một thành phố. RampNet không phủ toàn Manhattan; vùng thiếu dữ liệu phải được báo là “không biết”, không được tự xem là an toàn.

### 5.9. Đánh giá lựa chọn

Rất mới và có tiềm năng paper tốt, nhưng rủi ro triển khai cao. Chỉ nên chọn khi sẵn sàng đầu tư mạnh vào thuật toán và chấp nhận rằng kết quả khoa học hợp lệ có thể là xác định giới hạn của transfer thay vì luôn chứng minh cải thiện lớn.

---

## 6. T4 – BeGo-PARETO-MP: solver Pareto chạy “càng lâu càng tốt” và biết mình cách tối ưu bao xa

### 6.1. Hiểu đơn giản

Một solver thông thường có thể chạy lâu rồi mới trả kết quả, hoặc trả một kết quả mà ta không biết tốt đến đâu. Solver “anytime” sẽ:

1. trả một phương án hợp lệ sớm;
2. tiếp tục cải thiện khi còn thời gian;
3. cho biết lời giải hiện tại cách mức tối ưu nhiều nhất bao nhiêu;
4. đưa ra nhiều phương án đánh đổi thay vì một điểm số duy nhất.

### 6.2. Bối cảnh và động lực

Bài toán DARP và pickup-delivery là bài toán cổ điển. Tuy nhiên hệ thống thực tế cần vừa phản hồi nhanh vừa minh bạch về chất lượng. BeGo hiện có nhiều heuristic và giới hạn trạng thái nhưng không xuất cận, optimality gap hoặc lý do dừng. Cách benchmark hiện tại còn làm mất một phần ngữ nghĩa time window, pickup-delivery và meeting point.

### 6.3. Đây là ngách nào và có quá cũ không?

Nền bài toán là cũ và ổn định; đây vừa là ưu điểm vừa là nhược điểm.

- Ưu điểm: có benchmark chuẩn, baseline mạnh và tiêu chuẩn đánh giá rõ.
- Nhược điểm: nếu chỉ viết thêm một heuristic giảm tổng quãng đường thì rất dễ bị xem là không mới.

Ngách hiện đại nằm ở **anytime + chứng nhận gap + Pareto nhiều mục tiêu + giữ nguyên đầy đủ DARP-MP semantics**. Đây là hướng Operations Research/algorithm engineering, không chạy theo trào lưu AI nhưng học thuật chắc.

### 6.4. Cơ sở báo cáo và dữ liệu

- 23 paper trong corpus có sử dụng hoặc thảo luận Cordeau DARP.
- Các phương pháp branch-and-cut, local search, ALNS và multiobjective DARP tạo nền lý thuyết.
- [Benchmark DARP với meeting points](https://data.mendeley.com/datasets/h5392z6csr) được công bố năm 2023, giấy phép CC BY 4.0.
- [Li & Lim PDPTW](https://www.sintef.no/projectweb/top/pdptw/li-lim-benchmark/) là benchmark chuẩn cho pickup-delivery có time window.

### 6.5. Nhu cầu được giải quyết

- Cần kế hoạch hợp lệ ngay cả khi thời gian tính toán rất ngắn.
- Cần biết cải thiện thêm có còn đáng chờ không.
- Cần so sánh công bằng giữa thuật toán trên cùng ngữ nghĩa.
- Cần đưa cho người dùng/người vận hành nhiều phương án đánh đổi dễ hiểu.

### 6.6. Mục tiêu và đóng góp

- Mô hình đầy đủ pickup trước delivery, capacity, time window, service time, ride-time và loại meeting point.
- ALNS tạo lời giải đầu nhanh; CP-SAT/MIP cung cấp cận cho trường hợp giải được.
- Kho lưu các phương án không bị trội theo chi phí xe, đi bộ và burden lớn nhất.
- Đồ thị quá trình cải thiện theo thời gian.
- Bộ evaluator độc lập kiểm tra mọi lời giải, tránh thuật toán tự chấm chính mình.

### 6.7. Cơ sở lý thuyết dễ hiểu

- **Mixed Integer Programming – quy hoạch nguyên:** mô tả quyết định có/không bằng biến 0–1 và dùng solver tìm tối ưu.
- **CP-SAT:** solver kết hợp ràng buộc logic và tối ưu số nguyên, phù hợp bài toán lịch/route rời rạc.
- **Branch-and-bound/cut:** chia không gian lời giải và dùng cận để bỏ những vùng chắc chắn không thể tốt hơn.
- **ALNS:** nhanh chóng tạo lời giải tốt cho instance lớn nhưng thường không chứng minh tối ưu.
- **Epsilon-constraint:** chọn một mục tiêu để tối ưu, biến các mục tiêu còn lại thành giới hạn; lặp nhiều mức để dựng Pareto frontier.
- **Anytime algorithm:** có thể dừng bất cứ lúc nào và vẫn có kết quả hợp lệ.

### 6.8. Cách chứng minh

Chia family DARP-MP:

- `a2-*` để tinh chỉnh;
- `a3-*` để lựa chọn cấu hình;
- `a4-*` để kiểm tra khả năng tăng quy mô.

Dùng Li & Lim cho clustered, random và mixed; dùng Manhattan 2 giờ để kiểm tra tính thực tế.

Baseline:

- OR-Tools mô hình tương đương;
- CP-SAT/MIP chính xác cho instance nhỏ;
- ALNS cost-only;
- NSGA-II hoặc thuật toán tiến hóa nhiều mục tiêu;
- OptiGo hiện tại;
- best-known solution khi nguồn benchmark có công bố.

Đánh giá không chỉ chi phí cuối mà còn time-to-first-feasible, chất lượng theo từng mốc thời gian, hypervolume Pareto, gap, timeout và vi phạm ngữ nghĩa.

### 6.9. Điều cần tránh

Không được lấy cận của mô hình đơn giản rồi gọi đó là gap của bài toán đầy đủ. Không được để OR-Tools/PyVRP nhận một bài toán khác với BeGo. “Fairness tốt hơn” chỉ hợp lệ nếu phương án thật sự không bị trội trên các mục tiêu đã khai báo.

### 6.10. Đánh giá lựa chọn

Đây là lựa chọn chắc nhất nếu muốn đề tài thiên về toán tối ưu, ít phụ thuộc dữ liệu hành vi và dễ bảo vệ phương pháp. Nó không hào nhoáng như deep learning nhưng có tính tái lập và khả năng chứng minh rất cao.

---

## 7. T5 – BeGo-ACCESS: lộ trình và điểm đón theo từng loại thiết bị hỗ trợ di chuyển

### 7.1. Hiểu đơn giản

Một vỉa hè có thể đi được với người dùng gậy nhưng rất khó với xe lăn điện. Một curb ramp hơi dốc có thể chấp nhận được cho nhóm này nhưng nguy hiểm cho nhóm khác. Do đó “accessible route” không nên chỉ là một tuyến chung cho tất cả.

BeGo-ACCESS sẽ xây chi phí đường đi khác nhau cho người dùng gậy, walker, scooter, xe lăn tay và xe lăn điện, sau đó chọn tuyến đi bộ hoặc điểm xe đón phù hợp.

### 7.2. Bối cảnh và động lực

BeGo hiện kiểm tra quãng đường đi bộ chủ yếu theo khoảng cách thẳng và giới hạn chung. Điều này bỏ qua curb ramp bị thiếu, mặt đường xấu, vật cản và sự khác biệt giữa thiết bị hỗ trợ.

Nhu cầu thực tế rất rõ: tuyến ngắn nhất có thể là tuyến không sử dụng được. Nếu hệ thống chọn điểm đón “gần” nhưng người dùng không thể tới đó, toàn bộ kế hoạch thất bại.

### 7.3. Ngách và độ mới

Đây là giao điểm giữa:

- computer vision nhận diện curb ramp;
- human-centered computing thu thập đánh giá từ người có trải nghiệm thật;
- định tuyến có ràng buộc và uncertainty;
- lựa chọn điểm đón cho shared mobility.

Hai paper nền đều năm 2025, nên đề tài rất mới. Điểm mạnh là dữ liệu không chỉ do người không khuyết tật tự gán nhãn; có 190 người thuộc năm nhóm thiết bị hỗ trợ.

### 7.4. Báo cáo nền

- [Accessibility for Whom? – CHI 2025](https://homes.cs.washington.edu/~ypang2/papers/chi-2025-accessibility.pdf) chỉ ra cùng một rào cản được các nhóm cảm nhận khác nhau. [Dữ liệu và code](https://github.com/makeabilitylab/accessibility-for-whom) được công khai theo MIT.
- [RampNet 2025](https://arxiv.org/pdf/2508.09415) xây pipeline nhận diện curb ramp và công bố benchmark lớn.
- [RampNet dataset](https://huggingface.co/datasets/projectsidewalk/rampnet-dataset) có hơn 214 nghìn panorama và bộ gold 1.000 panorama gán nhãn thủ công.

### 7.5. Nhu cầu và giải pháp

- Thay khoảng cách thẳng bằng mạng đi bộ thật.
- Phát hiện curb ramp/vật cản từ ảnh hoặc nhãn crowdsourcing.
- Học mức nghiêm trọng riêng cho từng nhóm.
- Khi dữ liệu không chắc, chọn tuyến thận trọng hoặc báo “chưa đủ thông tin”.
- Chọn điểm xe có thể tiếp cận, không chỉ gần xe.

### 7.6. Mục tiêu và đóng góp

Đề tài cần tách ba lớp:

1. **Lớp nhìn:** phát hiện curb ramp/rào cản.
2. **Lớp hiểu người dùng:** chuyển đánh giá của từng nhóm thành xác suất hoặc chi phí không đi qua được.
3. **Lớp quyết định:** tìm tuyến/điểm đón giảm nguy cơ dưới giới hạn detour.

Đóng góp là liên kết ba lớp có kiểm định riêng, đồng thời truyền độ không chắc chắn từ mô hình ảnh tới quyết định route.

### 7.7. Cơ sở lý thuyết

- **Object detection – phát hiện vật thể:** tìm vị trí curb ramp trong ảnh và độ tin cậy.
- **Calibration – hiệu chỉnh độ tin cậy:** nếu mô hình nói 80% thì trong nhiều trường hợp tương tự nó nên đúng gần 80%.
- **Mixed-effects model – mô hình hiệu ứng hỗn hợp:** phân biệt khác nhau giữa nhóm, giữa người và giữa ảnh, tránh xem mọi đánh giá là độc lập.
- **Constrained shortest path – đường ngắn nhất có ràng buộc:** tuyến có thể dài hơn một chút nhưng xác suất gặp rào cản nặng phải dưới ngưỡng.
- **Chance constraint – ràng buộc xác suất:** kiểm soát nguy cơ thay vì giả vờ mọi nhãn đều chắc chắn.

### 7.8. Dataset và cách chứng minh

Lớp ảnh dùng đúng split chính thức của RampNet: train 150k, validation 42,9k, test 21,4k và bộ gold gán tay. Lớp người dùng dùng cross-validation tách theo participant, không để câu trả lời của cùng một người xuất hiện cả train và test.

Baseline route:

- tuyến ngắn nhất;
- một mức phạt accessibility chung;
- severity có sẵn của Project Sidewalk;
- tag wheelchair của OSM/Wheelmap;
- quy tắc của prototype CHI;
- mô hình theo nhóm nhưng không dùng uncertainty.

Chứng minh theo từng tầng:

- mAP/F1 và calibration cho detection;
- log loss/AUC cho dự đoán passability;
- số rào cản nặng trên tuyến, detour, tỷ lệ không tìm được tuyến và độ phủ cho quyết định route.

### 7.9. Điều được và không được nói

Có thể nói phương pháp giảm tiếp xúc với những loại rào cản được dataset đại diện. Không thể khẳng định phù hợp mọi dạng khuyết tật hay mọi thành phố. “Không có nhãn” không đồng nghĩa với “không có rào cản”.

### 7.10. Đánh giá lựa chọn

Đây là đề tài mới, có ý nghĩa xã hội và có câu chuyện nghiên cứu mạnh. Khó khăn chính là dữ liệu địa lý không đồng đều và cần hiểu cả ML lẫn routing. Nếu làm chỉnh chu, đây là lựa chọn khác biệt nhất so với đề tài tối ưu xe truyền thống.

---

## 8. T6 – BeGo-XETA: dự đoán ETA có khoảng tin cậy và dùng nó để chọn lộ trình

### 8.1. Hiểu đơn giản

Hai tuyến đều được dự đoán mất 20 phút, nhưng:

- tuyến A thường dao động 19–22 phút;
- tuyến B có thể mất 15 phút hoặc 35 phút.

Nếu nhóm phải tới đúng giờ, chỉ nhìn ETA trung bình sẽ chọn sai. BeGo-XETA dự đoán một khoảng thời gian có độ tin cậy rồi chọn phương án giảm nguy cơ trễ.

### 8.2. Bối cảnh và động lực

ETA là bài toán lâu đời và có rất nhiều mô hình. Nhiều nghiên cứu chỉ tối ưu MAE – sai số tuyệt đối trung bình. Nhưng một mô hình MAE tốt hơn chưa chắc giúp chọn route tốt hơn, đặc biệt khi sai số không đều hoặc chuyển sang thành phố khác.

### 8.3. Ngách và mức độ mới

ETA nói chung là lĩnh vực đông và có nguy cơ “quá cũ”. Để đề tài mới, phải có đủ ba thành phần:

1. khoảng dự đoán đã hiệu chỉnh, không chỉ một con số;
2. đánh giá chuyển thành phố Porto → Beijing/T-Drive;
3. đo tác động tới quyết định chọn route và trễ giờ.

Nếu chỉ xây thêm một Transformer giảm MAE trên Porto thì không đủ mới.

### 8.4. Paper nền

[DSETA: Driving Style-Aware Estimated Time of Arrival, CIKM 2025](https://liuzhidan.github.io/files/2025-CIKM-DSETA.pdf) dùng 1,5 triệu chuyến Shanghai, split theo thời gian và so với Transformer, MURAT, WDR, ProbTTE, CoDriver. Paper cho protocol và ý tưởng driver style tốt, nhưng dataset Shanghai không public; do đó không thể dùng làm dữ liệu chính của luận văn tái lập.

Đề tài thay thế bằng:

- [Porto Taxi UCI](https://archive.ics.uci.edu/dataset/339/taxi+service+trajectory+prediction+challenge+ecml+pkdd+2015): 1.710.671 chuyến, 442 taxi, GPS mỗi 15 giây, CC BY 4.0.
- [Microsoft T-Drive](https://www.microsoft.com/en-us/research/publication/t-drive-trajectory-data-sample/): 10.357 taxi, khoảng 15 triệu điểm GPS.

### 8.5. Nhu cầu được giải quyết

- Dự đoán nguy cơ trễ thay vì chỉ thời gian trung bình.
- Giúp chọn route ổn định cho nhóm cần tới gần cùng giờ.
- Biết mô hình có còn đáng tin khi chuyển thành phố hoặc gặp taxi chưa thấy trước đó.
- Phát hiện trường hợp mô hình không chắc để dùng phương án an toàn hơn.

### 8.6. Mục tiêu và đóng góp

- Mô hình dự đoán nhiều quantile, ví dụ mốc 10%, 50%, 90%.
- Hiệu chỉnh bằng conformal prediction để khoảng đạt tỷ lệ bao phủ cam kết.
- Lớp chọn route dùng toàn bộ phân phối ETA.
- Cross-city test và unseen-driver test.
- Đánh giá cả prediction và quyết định downstream.

### 8.7. Cơ sở lý thuyết

- **Map matching:** ghép chuỗi GPS nhiễu vào các đoạn đường thực trên bản đồ.
- **Quantile regression:** dự đoán các mốc của phân phối thay vì chỉ trung bình.
- **Conformal prediction:** dùng tập hiệu chỉnh để tạo khoảng dự đoán có bảo đảm bao phủ dưới các giả định phù hợp.
- **Decision-focused learning:** đánh giá/tối ưu mô hình theo chất lượng quyết định cuối cùng, không chỉ sai số dự đoán.
- **Distribution shift – dịch chuyển phân phối:** dữ liệu test khác thời gian, tài xế hoặc thành phố so với train.

### 8.8. Cách chứng minh

Dùng split thời gian của Porto; khóa bộ test challenge và solution. Tách thêm taxi chưa xuất hiện trong train. T-Drive là external test không dùng để lựa chọn mô hình.

Baseline:

- thời gian free-flow/OSRM;
- trung bình lịch sử theo đoạn đường–giờ;
- XGBoost/LightGBM;
- Transformer;
- DeepTTE/ProbTTE/CoDriver-style;
- ETA điểm cộng một safety margin cố định.

Đo MAE/RMSE/MAPE, coverage và độ rộng khoảng, sau đó đo route regret, tỷ lệ tới đúng giờ và worst-member lateness. Đề tài chỉ thành công nếu khoảng vừa đúng coverage vừa giúp giảm quyết định sai.

### 8.9. Giới hạn

Porto và T-Drive đều là dữ liệu cũ. Chúng đủ để chứng minh phương pháp và khả năng chuyển miền, không chứng minh độ chính xác giao thông hiện tại. Không nên đưa thời tiết nếu không có nguồn cùng thời gian cho cả hai thành phố.

### 8.10. Đánh giá lựa chọn

Phù hợp nếu muốn đề tài ML rõ nét, có dataset lớn và tiêu chuẩn đánh giá mạnh. Cạnh tranh nghiên cứu cao hơn T1/T4/T5; contribution phải nhấn vào calibration và decision quality.

---

## 9. T7 – BeGo-XDISPATCH: dự báo nhu cầu để điều xe, kiểm tra chuyển từ New York sang Chicago

### 9.1. Hiểu đơn giản

Nếu dự báo 15 phút tới khu A sẽ có nhiều khách, hệ thống có thể đưa xe rảnh tới gần đó. Nhưng một mô hình dự báo có MAE thấp chưa chắc giúp điều xe tốt: sai vài chuyến ở khu đông có thể ít quan trọng, trong khi bỏ sót một vùng hoàn toàn không có xe gây chờ rất lâu.

BeGo-XDISPATCH tối ưu dự báo theo hậu quả vận hành: phục vụ được bao nhiêu yêu cầu, khách chờ bao lâu và xe chạy rỗng bao nhiêu.

### 9.2. Bối cảnh và động lực

Demand forecasting và repositioning là hai lĩnh vực lớn. Nhiều pipeline làm tuần tự: dự báo càng chính xác càng tốt, sau đó đưa kết quả cho thuật toán điều xe. Hai bước không hiểu mục tiêu của nhau.

### 9.3. Ngách và độ mới

Forecast taxi là đề tài khá đông, có thể bị xem là cũ nếu chỉ so LSTM với Transformer. Ngách mới là:

- decision-focused: huấn luyện/tuning theo chi phí dispatch;
- robust: dùng phân phối/uncertainty thay vì một dự báo duy nhất;
- cross-city: kiểm tra New York → Chicago thay vì random split một thành phố;
- đo tail service theo khu vực nhưng không gán nhãn demographic giả.

### 9.4. Nền nghiên cứu

Đề tài tổng hợp ba dòng paper trong corpus: dự báo nhu cầu không gian–thời gian, tái bố trí xe và predict-then-optimize/decision-focused learning. Paper [Rethinking Pooled Ride-Hailing, 2026](https://eta-publications.lbl.gov/sites/default/files/2026-04/rethinking_pooled_ride-hailing.pdf) đặc biệt nhấn mạnh rằng repositioning quá mạnh có thể tăng deadheading, VMT và năng lượng dù cải thiện phục vụ rất ít.

### 9.5. Dataset công khai

- [NYC TLC Trip Records](https://www.nyc.gov/site/tlc/about/tlc-trip-record-data.page): dữ liệu hàng tháng từ 2009, có thời gian, vùng đón/trả, khoảng cách, fare và passenger count.
- [Chicago Taxi Trips](https://catalog.data.gov/dataset/taxi-trips-2024): dữ liệu thành phố công khai, nhưng vị trí được làm tròn/ẩn một phần vì riêng tư.
- OSM cho chi phí đi giữa các zone.

### 9.6. Mục tiêu và đóng góp

- Mô hình dự báo xác suất nhu cầu mỗi zone và mỗi 15 phút.
- Tối ưu receding horizon: cứ một khoảng thời gian lại cập nhật và điều chỉnh kế hoạch xe.
- Loss phản ánh chi phí dispatch, không chỉ MAE.
- Robust optimization để tránh điều quá nhiều xe theo một dự báo không chắc.
- External-city test để đo khả năng tổng quát.

### 9.7. Cơ sở lý thuyết

- **Spatiotemporal forecasting:** dự báo vừa theo thời gian vừa theo quan hệ lân cận không gian.
- **Graph neural network:** biểu diễn zone như các nút, quan hệ di chuyển/lân cận như cạnh.
- **Min-cost flow – luồng chi phí nhỏ nhất:** quyết định chuyển bao nhiêu xe giữa các zone với tổng chi phí thấp.
- **Distributionally robust optimization:** tìm quyết định vẫn tốt khi phân phối nhu cầu thực hơi khác dự báo.
- **Receding horizon:** chỉ thực hiện phần đầu kế hoạch rồi cập nhật lại khi có dữ liệu mới.

### 9.8. Cách chứng minh

NYC chia theo các tháng liên tiếp, ví dụ Jan–Jun train, Jul–Sep validation, Oct–Dec test; sau đó rolling-origin qua các năm. Chicago là external test với độ phân giải zone phù hợp mức làm mờ.

Baseline dự báo: historical average, seasonal naive, XGBoost, STGCN, DCRNN, Graph WaveNet/TFT. Baseline dispatch: không reposition, đưa xe tới zone gần nhất thiếu xe, theo tỷ lệ forecast, min-cost flow và oracle biết tương lai.

Đo hai tầng:

- dự báo: MAE, RMSE, WAPE, CRPS;
- vận hành: served rate, chờ trung bình/p95, deadhead VMT, occupied VMT, utilization và worst-decile zone deficit.

Muốn chứng minh tốt, phương pháp phải cải thiện dispatch trên NYC test và giữ được phần đáng kể lợi ích khi chuyển sang Chicago. MAE thấp hơn một mình không đủ.

### 9.9. Giới hạn

Dữ liệu chỉ có chuyến đã xảy ra, không chứa người yêu cầu nhưng không có xe phục vụ. Vì vậy kết quả là counterfactual replay trên observed demand, không phải ước lượng toàn bộ nhu cầu tiềm ẩn. Chicago làm mờ tọa độ nên không dùng đánh giá ở cấp curb.

### 9.10. Đánh giá lựa chọn

Dữ liệu rất mạnh và có giá trị thực tế. Tuy nhiên đây là lĩnh vực cạnh tranh; khối lượng ML, simulator và optimization đều lớn. Nên chọn nếu muốn đề tài quy mô lớn và có năng lực tính toán tốt.

---

## 10. T8 – BeGo-TRANSITSYNC: xe gom khách đồng bộ với lịch tàu/bus

### 10.1. Hiểu đơn giản

Ba người ở gần nhau đều muốn tới ga. Nếu xe đưa họ tới ga sau khi tàu vừa chạy, cả kế hoạch kém dù tuyến xe ngắn. Hệ thống phải chọn:

- ga nào;
- chuyến tàu/bus cụ thể nào;
- ghép những ai cùng xe;
- xe xuất phát lúc nào;
- cần buffer bao nhiêu để không lỡ chuyến.

### 10.2. Bối cảnh và động lực

Ride-hailing điểm-tới-điểm gây nhiều xe chạy rỗng. Transit hiệu quả trên hành lang đông nhưng khó phục vụ đoạn đầu/cuối. Kết hợp feeder linh hoạt với transit là hướng có nhu cầu thực tế, đặc biệt ngoài giờ cao điểm hoặc khu dân cư xa ga.

### 10.3. Ngách và độ mới

First-mile/last-mile đã được nghiên cứu nhiều. Ngách đề tài là **joint optimization của ga, chuyến GTFS, nhóm hành khách và route**, cùng với missed-connection risk. Nếu chỉ “đưa người tới ga gần nhất” thì không mới.

### 10.4. Paper nền

[Semi-on-Demand Transit Feeders with Shared Autonomous Vehicles and Reinforcement-Learning-Based Zonal Dispatching Control, 2025](https://arxiv.org/pdf/2509.01883) cho thấy zonal RL có thể tăng số người phục vụ trong mô phỏng Munich. Tuy nhiên paper dùng boarding/alighting riêng của nhà vận hành và giả định nhu cầu thay đổi theo thời gian đi bộ.

Đề tài mới dùng public taxi OD làm request quan sát và public GTFS làm lịch, đồng thời giới hạn tuyên bố ở “tiềm năng vận hành”.

### 10.5. Dataset

- [NYC TLC](https://www.nyc.gov/site/tlc/about/tlc-trip-record-data.page) để lấy chuyến có đầu/cuối trong catchment quanh ga.
- [MTA GTFS static](https://catalog.data.gov/dataset/mta-general-transit-feed-specification-gtfs-static-data) cho stop, route, trip và schedule.
- OSM cho mạng đường feeder.

### 10.6. Nhu cầu, mục tiêu và đóng góp

- Giảm quãng đường xe so với taxi đi toàn tuyến.
- Hạn chế missed connection.
- Không ép mọi người tới ga gần nhất nếu ga khác có chuyến phù hợp hơn.
- Mô hình time window được tạo từ chuyến transit cụ thể.
- Thuật toán exact trên cửa sổ nhỏ và rolling heuristic trên cửa sổ lớn.

### 10.7. Cơ sở lý thuyết

- **GTFS:** chuẩn dữ liệu mô tả tuyến, điểm dừng và lịch giao thông công cộng.
- **Time-dependent routing:** chất lượng route phụ thuộc thời điểm tới ga, không chỉ khoảng cách.
- **Feeder DARP:** xe theo yêu cầu gom khách và kết nối một hệ thống transit chính.
- **Transfer buffer:** khoảng dự phòng giữa lúc tới ga và lúc chuyến tiếp theo khởi hành.
- **Chance-constrained scheduling:** kiểm soát xác suất lỡ kết nối khi thời gian xe không chắc chắn.

### 10.8. Cách chứng minh

Chọn một năm TLC, chia Jan–Aug train/calibration, Sep–Oct validation, Nov–Dec test. Lưu chính xác file GTFS, ngày tải và hash. Nếu không có GTFS lịch sử tương ứng, phải nói đây là kịch bản counterfactual dùng một snapshot cố định.

Baseline:

- taxi đi thẳng;
- tới ga gần nhất, không pooling;
- fixed feeder route/headway;
- pooling nhưng không quan tâm timetable;
- timetable-aware nhưng không pooling;
- exact oracle trên instance nhỏ.

Metrics: connection success, missed connection, access/wait/in-vehicle time, generalized cost, vehicle VMT, served rate, capacity và runtime.

### 10.9. Điều không được tuyên bố

Một chuyến taxi kết thúc gần ga không chứng minh người đó muốn chuyển sang tàu. Đề tài chỉ chứng minh nếu xem các OD đó như nhu cầu feeder thì thuật toán vận hành hiệu quả thế nào. Muốn nói về adoption phải có khảo sát hoặc thử nghiệm người dùng.

### 10.10. Đánh giá lựa chọn

Câu chuyện hệ thống đẹp và có tính bền vững, nhưng cần rất cẩn thận về sự khớp thời gian giữa TLC và GTFS. Phù hợp nếu muốn hướng multimodal và có thể chấp nhận kết quả là phân tích counterfactual.

---

## 11. T9 – BeGo-ROUTEPOI: gợi ý điểm đến cá nhân có xét đường đi thực tế

### 11.1. Hiểu đơn giản

Một hệ thống có thể dự đoán người dùng thích một quán rất chính xác, nhưng quán nằm quá xa hoặc không thể tới trong thời gian còn lại. BeGo-ROUTEPOI trước tiên dự đoán các địa điểm có khả năng được chọn, sau đó xếp lại theo chi phí đường đi và giới hạn thời gian.

### 11.2. Bối cảnh và động lực

Next-POI recommendation thường được chấm bằng việc địa điểm thật có nằm trong top-k hay không. Cách này ít quan tâm tới hậu quả vận hành: đề xuất có cách 30 km không, có phù hợp route hiện tại không, có reachable trong thời gian hay không.

### 11.3. Ngách và độ mới

POI recommendation là lĩnh vực đông và không mới. Đề tài chỉ đủ mạnh khi tập trung vào:

- route-feasibility;
- accuracy–travel-cost Pareto frontier;
- decision-aware reranking;
- external-city validation;
- cá nhân thật, không random group.

### 11.4. Paper nền và cảnh báo quan trọng

[Beyond Individual and Point: Next POI Recommendation via Region-aware Dynamic Hypergraph, IJCAI 2025](https://www.ijcai.org/proceedings/2025/0343.pdf) dùng Foursquare NYC/Tokyo và Gowalla, split thời gian 80/10/10, so chín baseline và cho thấy region/trajectory giúp dữ liệu thưa. Nhưng paper vẫn tập trung vào ranking, chưa đo tính khả thi đường đi.

Paper Scientific Reports 2025 về POI cho nhóm ngẫu nhiên dùng check-in thật nhưng tự sinh Big-Five personality và group. Vì vậy tài liệu này **không** đề xuất group recommendation: làm như vậy sẽ quay lại lỗi dùng dữ liệu thật làm nền cho nhãn cốt lõi nhân tạo.

### 11.5. Dataset

- [Gowalla SNAP](https://snap.stanford.edu/data/loc-gowalla.html): 196.591 user, 6.442.890 check-in có thời gian và tọa độ.
- [Yelp Open Dataset](https://business.yelp.com/data/resources/open-dataset/): gần 7 triệu review, hơn 150 nghìn business và 11 vùng đô thị; dùng cho giáo dục.
- OSM cho route distance/time.

### 11.6. Nhu cầu và giải pháp

- Tránh đề xuất địa điểm không thực tế về đường đi.
- Hỗ trợ người dùng lịch sử thưa bằng thông tin vùng và category.
- Cho phép abstain – không đưa gợi ý chắc chắn khi dữ liệu quá ít.
- Cân bằng accuracy với chi phí di chuyển thay vì tối ưu một phía.

### 11.7. Mục tiêu và đóng góp

- Candidate generator dự đoán top-N POI.
- Route-aware reranker dùng khoảng cách/thời gian mạng đường.
- Objective có ràng buộc travel budget.
- Test transductive, unseen-user/POI và external city.
- Pareto frontier giữa hit rate và route cost.

### 11.8. Cơ sở lý thuyết

- **Sequential recommendation:** dự đoán lựa chọn tiếp theo từ chuỗi lịch sử.
- **Markov model:** địa điểm tiếp theo phụ thuộc mạnh vào trạng thái/địa điểm gần nhất.
- **Graph/hypergraph:** học quan hệ nhiều phía giữa user, POI, vùng và trajectory.
- **Learning to rank:** học thứ tự các ứng viên thay vì chỉ phân loại đúng/sai.
- **Constrained reranking:** xếp lại nhưng không cho accuracy hoặc travel budget vượt giới hạn.

### 11.9. Cách chứng minh

Gowalla split theo timestamp 80/10/10; khóa người/POI chưa thấy thành một test riêng. Yelp dùng một hoặc nhiều metropolitan area làm external test. Không random dòng dữ liệu vì sẽ rò rỉ tương lai.

Baseline:

- popularity và nearest POI;
- FPMC, PRME, STAN, GETNext, STHGCN, ReHDM-style;
- route-only;
- weighted reranking đơn giản.

Đo Acc@1/5/10, MRR, NDCG, feasibility rate, route cost, route regret, coverage và sparse-user performance. Phương pháp đạt nếu giữ accuracy gần baseline tốt nhất nhưng giảm rõ chi phí route, hoặc tạo Pareto frontier tốt hơn trên cả Gowalla và Yelp.

### 11.10. Giới hạn

Review Yelp là một lựa chọn business được bộc lộ, không phải mọi lần ghé thực tế. Check-in cũ không phản ánh địa điểm hiện tại. Đề tài chứng minh phương pháp recommendation, không chứng minh sở thích nhóm.

### 11.11. Đánh giá lựa chọn

Dễ gắn với phần chọn điểm đến của BeGo và có dữ liệu lớn. Tuy nhiên novelty thấp hơn T1/T4/T5 vì POI recommendation rất cạnh tranh. Cần giữ contribution tập trung ở route-aware decision quality.

---

## 12. So sánh nhanh để lựa chọn

| Đề tài | Phong cách nghiên cứu chính | Điểm mạnh | Rủi ro lớn nhất | Mức tái sử dụng BeGo | Khuyến nghị |
|---|---|---|---|---|---|
| T1 BeGo-CAST | Tối ưu + hệ thống | Data Manhattan rất khớp; đóng góp rõ; cân bằng | Không có nhãn willingness-to-walk | Rất cao | **Lựa chọn tổng thể tốt nhất** |
| T2 BeGo-WAIT | Online decision/RL | Câu hỏi hẹp, dễ đo, paper nền mới | Dễ thành “thêm RL” nếu thiết kế không sắc | Cao | Tốt nếu thích RL gọn |
| T3 BeGo-XFER | Thuật toán nâng cao | Rất mới; khoảng trống paper rõ | Khó triển khai, dữ liệu accessibility thiếu vùng | Trung bình–cao | Chọn khi chấp nhận rủi ro cao |
| T4 BeGo-PARETO-MP | Operations Research | Chứng minh mạnh, benchmark chuẩn, tái lập | Bài toán nền cổ điển; phải mới ở chứng nhận/Pareto | Rất cao | **Tốt nhất nếu thiên toán tối ưu** |
| T5 BeGo-ACCESS | HCI + CV + routing | Mới, có ý nghĩa xã hội, dữ liệu người dùng thật | Ba tầng kỹ thuật và độ phủ địa lý | Cao | **Tốt nhất nếu muốn khác biệt** |
| T6 BeGo-XETA | Machine Learning | Dataset lớn, protocol rõ, cross-city | ETA là lĩnh vực đông và dữ liệu cũ | Trung bình | Tốt nếu thiên ML |
| T7 BeGo-XDISPATCH | ML + optimization | Quy mô lớn, giá trị vận hành rõ | Khối lượng rất lớn; observed demand không phải total demand | Trung bình | Mạnh nhưng cần tài nguyên |
| T8 BeGo-TRANSITSYNC | Multimodal optimization | Câu chuyện bền vững và hệ thống đẹp | Lịch GTFS lịch sử và adoption không quan sát | Cao | Tốt nếu thích public transit |
| T9 BeGo-ROUTEPOI | Recommender + routing | Dữ liệu lớn, gần chọn điểm đến | Novelty thấp hơn, dữ liệu cũ | Trung bình–cao | An toàn nhưng không nổi bật nhất |

## 13. Đề xuất thứ tự ưu tiên cuối cùng

### Ưu tiên 1 – T1 BeGo-CAST

T1 phù hợp nhất với tài sản hiện tại của BeGo, có benchmark mới năm 2026, có thể chứng minh bằng chi phí vận hành thật và vẫn giữ được câu chuyện fairness ở mức **giới hạn gánh nặng trong một chuyến**. Đây là fairness có thể đo trực tiếp, không cần tự sinh lịch sử nhóm.

### Ưu tiên 2 – T4 BeGo-PARETO-MP

Nếu muốn luận văn chắc về thuật toán, T4 có lợi thế lớn: feasibility, Pareto, runtime và optimality gap đều kiểm tra được trên benchmark công khai. Kết quả dễ tái lập và ít phụ thuộc giả định hành vi.

### Ưu tiên 3 – T5 BeGo-ACCESS

Nếu muốn một đề tài mới, có tác động xã hội và khác biệt rõ so với routing thông thường, T5 là lựa chọn hấp dẫn nhất. Cần kiểm soát phạm vi để không biến thành ba luận văn cùng lúc.

## 14. Kết luận về việc “tự sinh dữ liệu có bias không?”

Tự sinh dữ liệu không phải lúc nào cũng sai. Nó phù hợp cho:

- kiểm tra trường hợp biên;
- test invariant như không vượt capacity;
- stress test khi tăng số người/xe;
- phân tích cơ chế trong điều kiện kiểm soát.

Nó trở nên bias khi dữ liệu sinh là bằng chứng duy nhất cho chính tuyên bố trung tâm, đặc biệt nếu quy tắc sinh dùng cùng giả định với thuật toán đề xuất. Quy trình đúng cho cả chín đề tài là:

1. kết quả chính trên public dataset chưa dùng để thiết kế thuật toán;
2. external dataset hoặc thành phố/instance family khác;
3. synthetic chỉ là stress test bổ sung;
4. cùng evaluator, constraint và compute budget cho mọi baseline;
5. công bố cả trường hợp phương pháp không thắng.

## 15. Tài liệu liên quan trong workspace

- [Báo cáo kỹ thuật và protocol của 9 đề tài](bego-public-data-first-9-topics.md)
- [Evidence matrix của các paper đọc bổ sung](bego-public-data-targeted-paper-evidence.md)
- [Audit dataset trong 170 paper](sources/public-dataset-corpus-audit.md)
- [Audit hệ thống BeGo hiện tại](sources/current-system-audit.md)
