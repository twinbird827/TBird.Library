using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netkeiba
{
	public partial class PredictionSource
	{
		private Dictionary<int, Action<float>> dic;

		public PredictionSource()
		{
			dic = new Dictionary<int, Action<float>>()
			{
				{ 0, x =>  C0000 = x },
				{ 1, x =>  C0001 = x },
				{ 2, x =>  C0002 = x },
				{ 3, x =>  C0003 = x },
				{ 4, x =>  C0004 = x },
				{ 5, x =>  C0005 = x },
				{ 6, x =>  C0006 = x },
				{ 7, x =>  C0007 = x },
				{ 8, x =>  C0008 = x },
				{ 9, x =>  C0009 = x },
				{ 10, x =>  C0010 = x },
				{ 11, x =>  C0011 = x },
				{ 12, x =>  C0012 = x },
				{ 13, x =>  C0013 = x },
				{ 14, x =>  C0014 = x },
				{ 15, x =>  C0015 = x },
				{ 16, x =>  C0016 = x },
				{ 17, x =>  C0017 = x },
				{ 18, x =>  C0018 = x },
				{ 19, x =>  C0019 = x },
				{ 20, x =>  C0020 = x },
				{ 21, x =>  C0021 = x },
				{ 22, x =>  C0022 = x },
				{ 23, x =>  C0023 = x },
				{ 24, x =>  C0024 = x },
				{ 25, x =>  C0025 = x },
				{ 26, x =>  C0026 = x },
				{ 27, x =>  C0027 = x },
				{ 28, x =>  C0028 = x },
				{ 29, x =>  C0029 = x },
				{ 30, x =>  C0030 = x },
				{ 31, x =>  C0031 = x },
				{ 32, x =>  C0032 = x },
				{ 33, x =>  C0033 = x },
				{ 34, x =>  C0034 = x },
				{ 35, x =>  C0035 = x },
				{ 36, x =>  C0036 = x },
				{ 37, x =>  C0037 = x },
				{ 38, x =>  C0038 = x },
				{ 39, x =>  C0039 = x },
				{ 40, x =>  C0040 = x },
				{ 41, x =>  C0041 = x },
				{ 42, x =>  C0042 = x },
				{ 43, x =>  C0043 = x },
				{ 44, x =>  C0044 = x },
				{ 45, x =>  C0045 = x },
				{ 46, x =>  C0046 = x },
				{ 47, x =>  C0047 = x },
				{ 48, x =>  C0048 = x },
				{ 49, x =>  C0049 = x },
				{ 50, x =>  C0050 = x },
				{ 51, x =>  C0051 = x },
				{ 52, x =>  C0052 = x },
				{ 53, x =>  C0053 = x },
				{ 54, x =>  C0054 = x },
				{ 55, x =>  C0055 = x },
				{ 56, x =>  C0056 = x },
				{ 57, x =>  C0057 = x },
				{ 58, x =>  C0058 = x },
				{ 59, x =>  C0059 = x },
				{ 60, x =>  C0060 = x },
				{ 61, x =>  C0061 = x },
				{ 62, x =>  C0062 = x },
				{ 63, x =>  C0063 = x },
				{ 64, x =>  C0064 = x },
				{ 65, x =>  C0065 = x },
				{ 66, x =>  C0066 = x },
				{ 67, x =>  C0067 = x },
				{ 68, x =>  C0068 = x },
				{ 69, x =>  C0069 = x },
				{ 70, x =>  C0070 = x },
				{ 71, x =>  C0071 = x },
				{ 72, x =>  C0072 = x },
				{ 73, x =>  C0073 = x },
				{ 74, x =>  C0074 = x },
				{ 75, x =>  C0075 = x },
				{ 76, x =>  C0076 = x },
				{ 77, x =>  C0077 = x },
				{ 78, x =>  C0078 = x },
				{ 79, x =>  C0079 = x },
				{ 80, x =>  C0080 = x },
				{ 81, x =>  C0081 = x },
				{ 82, x =>  C0082 = x },
				{ 83, x =>  C0083 = x },
				{ 84, x =>  C0084 = x },
				{ 85, x =>  C0085 = x },
				{ 86, x =>  C0086 = x },
				{ 87, x =>  C0087 = x },
				{ 88, x =>  C0088 = x },
				{ 89, x =>  C0089 = x },
				{ 90, x =>  C0090 = x },
				{ 91, x =>  C0091 = x },
				{ 92, x =>  C0092 = x },
				{ 93, x =>  C0093 = x },
				{ 94, x =>  C0094 = x },
				{ 95, x =>  C0095 = x },
				{ 96, x =>  C0096 = x },
				{ 97, x =>  C0097 = x },
				{ 98, x =>  C0098 = x },
				{ 99, x =>  C0099 = x },
				{ 100, x =>  C0100 = x },
				{ 101, x =>  C0101 = x },
				{ 102, x =>  C0102 = x },
				{ 103, x =>  C0103 = x },
				{ 104, x =>  C0104 = x },
				{ 105, x =>  C0105 = x },
				{ 106, x =>  C0106 = x },
				{ 107, x =>  C0107 = x },
				{ 108, x =>  C0108 = x },
				{ 109, x =>  C0109 = x },
				{ 110, x =>  C0110 = x },
				{ 111, x =>  C0111 = x },
				{ 112, x =>  C0112 = x },
				{ 113, x =>  C0113 = x },
				{ 114, x =>  C0114 = x },
				{ 115, x =>  C0115 = x },
				{ 116, x =>  C0116 = x },
				{ 117, x =>  C0117 = x },
				{ 118, x =>  C0118 = x },
				{ 119, x =>  C0119 = x },
				{ 120, x =>  C0120 = x },
				{ 121, x =>  C0121 = x },
				{ 122, x =>  C0122 = x },
				{ 123, x =>  C0123 = x },
				{ 124, x =>  C0124 = x },
				{ 125, x =>  C0125 = x },
				{ 126, x =>  C0126 = x },
				{ 127, x =>  C0127 = x },
				{ 128, x =>  C0128 = x },
				{ 129, x =>  C0129 = x },
				{ 130, x =>  C0130 = x },
				{ 131, x =>  C0131 = x },
				{ 132, x =>  C0132 = x },
				{ 133, x =>  C0133 = x },
				{ 134, x =>  C0134 = x },
				{ 135, x =>  C0135 = x },
				{ 136, x =>  C0136 = x },
				{ 137, x =>  C0137 = x },
				{ 138, x =>  C0138 = x },
				{ 139, x =>  C0139 = x },
				{ 140, x =>  C0140 = x },
				{ 141, x =>  C0141 = x },
				{ 142, x =>  C0142 = x },
				{ 143, x =>  C0143 = x },
				{ 144, x =>  C0144 = x },
				{ 145, x =>  C0145 = x },
				{ 146, x =>  C0146 = x },
				{ 147, x =>  C0147 = x },
				{ 148, x =>  C0148 = x },
				{ 149, x =>  C0149 = x },
				{ 150, x =>  C0150 = x },
				{ 151, x =>  C0151 = x },
				{ 152, x =>  C0152 = x },
				{ 153, x =>  C0153 = x },
				{ 154, x =>  C0154 = x },
				{ 155, x =>  C0155 = x },
				{ 156, x =>  C0156 = x },
				{ 157, x =>  C0157 = x },
				{ 158, x =>  C0158 = x },
				{ 159, x =>  C0159 = x },
				{ 160, x =>  C0160 = x },
				{ 161, x =>  C0161 = x },
				{ 162, x =>  C0162 = x },
				{ 163, x =>  C0163 = x },
				{ 164, x =>  C0164 = x },
				{ 165, x =>  C0165 = x },
				{ 166, x =>  C0166 = x },
				{ 167, x =>  C0167 = x },
				{ 168, x =>  C0168 = x },
				{ 169, x =>  C0169 = x },
				{ 170, x =>  C0170 = x },
				{ 171, x =>  C0171 = x },
				{ 172, x =>  C0172 = x },
				{ 173, x =>  C0173 = x },
				{ 174, x =>  C0174 = x },
				{ 175, x =>  C0175 = x },
				{ 176, x =>  C0176 = x },
				{ 177, x =>  C0177 = x },
				{ 178, x =>  C0178 = x },
				{ 179, x =>  C0179 = x },
				{ 180, x =>  C0180 = x },
				{ 181, x =>  C0181 = x },
				{ 182, x =>  C0182 = x },
				{ 183, x =>  C0183 = x },
				{ 184, x =>  C0184 = x },
				{ 185, x =>  C0185 = x },
				{ 186, x =>  C0186 = x },
				{ 187, x =>  C0187 = x },
				{ 188, x =>  C0188 = x },
				{ 189, x =>  C0189 = x },
				{ 190, x =>  C0190 = x },
				{ 191, x =>  C0191 = x },
				{ 192, x =>  C0192 = x },
				{ 193, x =>  C0193 = x },
				{ 194, x =>  C0194 = x },
				{ 195, x =>  C0195 = x },
				{ 196, x =>  C0196 = x },
				{ 197, x =>  C0197 = x },
				{ 198, x =>  C0198 = x },
				{ 199, x =>  C0199 = x },
				{ 200, x =>  C0200 = x },
				{ 201, x =>  C0201 = x },
				{ 202, x =>  C0202 = x },
				{ 203, x =>  C0203 = x },
				{ 204, x =>  C0204 = x },
				{ 205, x =>  C0205 = x },
				{ 206, x =>  C0206 = x },
				{ 207, x =>  C0207 = x },
				{ 208, x =>  C0208 = x },
				{ 209, x =>  C0209 = x },
				{ 210, x =>  C0210 = x },
				{ 211, x =>  C0211 = x },
				{ 212, x =>  C0212 = x },
				{ 213, x =>  C0213 = x },
				{ 214, x =>  C0214 = x },
				{ 215, x =>  C0215 = x },
				{ 216, x =>  C0216 = x },
				{ 217, x =>  C0217 = x },
				{ 218, x =>  C0218 = x },
				{ 219, x =>  C0219 = x },
				{ 220, x =>  C0220 = x },
				{ 221, x =>  C0221 = x },
				{ 222, x =>  C0222 = x },
				{ 223, x =>  C0223 = x },
				{ 224, x =>  C0224 = x },
				{ 225, x =>  C0225 = x },
				{ 226, x =>  C0226 = x },
				{ 227, x =>  C0227 = x },
				{ 228, x =>  C0228 = x },
				{ 229, x =>  C0229 = x },
				{ 230, x =>  C0230 = x },
				{ 231, x =>  C0231 = x },
				{ 232, x =>  C0232 = x },
				{ 233, x =>  C0233 = x },
				{ 234, x =>  C0234 = x },
				{ 235, x =>  C0235 = x },
				{ 236, x =>  C0236 = x },
				{ 237, x =>  C0237 = x },
				{ 238, x =>  C0238 = x },
				{ 239, x =>  C0239 = x },
				{ 240, x =>  C0240 = x },
				{ 241, x =>  C0241 = x },
				{ 242, x =>  C0242 = x },
				{ 243, x =>  C0243 = x },
				{ 244, x =>  C0244 = x },
				{ 245, x =>  C0245 = x },
				{ 246, x =>  C0246 = x },
				{ 247, x =>  C0247 = x },
				{ 248, x =>  C0248 = x },
				{ 249, x =>  C0249 = x },
				{ 250, x =>  C0250 = x },
				{ 251, x =>  C0251 = x },
				{ 252, x =>  C0252 = x },
				{ 253, x =>  C0253 = x },
				{ 254, x =>  C0254 = x },
				{ 255, x =>  C0255 = x },
				{ 256, x =>  C0256 = x },
				{ 257, x =>  C0257 = x },
				{ 258, x =>  C0258 = x },
				{ 259, x =>  C0259 = x },
				{ 260, x =>  C0260 = x },
				{ 261, x =>  C0261 = x },
				{ 262, x =>  C0262 = x },
				{ 263, x =>  C0263 = x },
				{ 264, x =>  C0264 = x },
				{ 265, x =>  C0265 = x },
				{ 266, x =>  C0266 = x },
				{ 267, x =>  C0267 = x },
				{ 268, x =>  C0268 = x },
				{ 269, x =>  C0269 = x },
				{ 270, x =>  C0270 = x },
				{ 271, x =>  C0271 = x },
				{ 272, x =>  C0272 = x },
				{ 273, x =>  C0273 = x },
				{ 274, x =>  C0274 = x },
				{ 275, x =>  C0275 = x },
				{ 276, x =>  C0276 = x },
				{ 277, x =>  C0277 = x },
				{ 278, x =>  C0278 = x },
				{ 279, x =>  C0279 = x },
				{ 280, x =>  C0280 = x },
				{ 281, x =>  C0281 = x },
				{ 282, x =>  C0282 = x },
				{ 283, x =>  C0283 = x },
				{ 284, x =>  C0284 = x },
				{ 285, x =>  C0285 = x },
				{ 286, x =>  C0286 = x },
				{ 287, x =>  C0287 = x },
				{ 288, x =>  C0288 = x },
				{ 289, x =>  C0289 = x },
				{ 290, x =>  C0290 = x },
				{ 291, x =>  C0291 = x },
				{ 292, x =>  C0292 = x },
				{ 293, x =>  C0293 = x },
				{ 294, x =>  C0294 = x },
				{ 295, x =>  C0295 = x },
				{ 296, x =>  C0296 = x },
				{ 297, x =>  C0297 = x },
				{ 298, x =>  C0298 = x },
				{ 299, x =>  C0299 = x },
				{ 300, x =>  C0300 = x },
				{ 301, x =>  C0301 = x },
				{ 302, x =>  C0302 = x },
				{ 303, x =>  C0303 = x },
				{ 304, x =>  C0304 = x },
				{ 305, x =>  C0305 = x },
				{ 306, x =>  C0306 = x },
				{ 307, x =>  C0307 = x },
				{ 308, x =>  C0308 = x },
				{ 309, x =>  C0309 = x },
				{ 310, x =>  C0310 = x },
				{ 311, x =>  C0311 = x },
				{ 312, x =>  C0312 = x },
				{ 313, x =>  C0313 = x },
				{ 314, x =>  C0314 = x },
				{ 315, x =>  C0315 = x },
				{ 316, x =>  C0316 = x },
				{ 317, x =>  C0317 = x },
				{ 318, x =>  C0318 = x },
				{ 319, x =>  C0319 = x },
				{ 320, x =>  C0320 = x },
				{ 321, x =>  C0321 = x },
				{ 322, x =>  C0322 = x },
				{ 323, x =>  C0323 = x },
				{ 324, x =>  C0324 = x },
				{ 325, x =>  C0325 = x },
				{ 326, x =>  C0326 = x },
				{ 327, x =>  C0327 = x },
				{ 328, x =>  C0328 = x },
				{ 329, x =>  C0329 = x },
				{ 330, x =>  C0330 = x },
				{ 331, x =>  C0331 = x },
				{ 332, x =>  C0332 = x },
				{ 333, x =>  C0333 = x },
				{ 334, x =>  C0334 = x },
				{ 335, x =>  C0335 = x },
				{ 336, x =>  C0336 = x },
				{ 337, x =>  C0337 = x },
				{ 338, x =>  C0338 = x },
				{ 339, x =>  C0339 = x },
				{ 340, x =>  C0340 = x },
				{ 341, x =>  C0341 = x },
				{ 342, x =>  C0342 = x },
				{ 343, x =>  C0343 = x },
				{ 344, x =>  C0344 = x },
				{ 345, x =>  C0345 = x },
				{ 346, x =>  C0346 = x },
				{ 347, x =>  C0347 = x },
				{ 348, x =>  C0348 = x },
				{ 349, x =>  C0349 = x },
				{ 350, x =>  C0350 = x },
				{ 351, x =>  C0351 = x },
				{ 352, x =>  C0352 = x },
				{ 353, x =>  C0353 = x },
				{ 354, x =>  C0354 = x },
				{ 355, x =>  C0355 = x },
				{ 356, x =>  C0356 = x },
				{ 357, x =>  C0357 = x },
				{ 358, x =>  C0358 = x },
				{ 359, x =>  C0359 = x },
				{ 360, x =>  C0360 = x },
				{ 361, x =>  C0361 = x },
				{ 362, x =>  C0362 = x },
				{ 363, x =>  C0363 = x },
				{ 364, x =>  C0364 = x },
				{ 365, x =>  C0365 = x },
				{ 366, x =>  C0366 = x },
				{ 367, x =>  C0367 = x },
				{ 368, x =>  C0368 = x },
				{ 369, x =>  C0369 = x },
				{ 370, x =>  C0370 = x },
				{ 371, x =>  C0371 = x },
				{ 372, x =>  C0372 = x },
				{ 373, x =>  C0373 = x },
				{ 374, x =>  C0374 = x },
				{ 375, x =>  C0375 = x },
				{ 376, x =>  C0376 = x },
				{ 377, x =>  C0377 = x },
				{ 378, x =>  C0378 = x },
				{ 379, x =>  C0379 = x },
				{ 380, x =>  C0380 = x },
				{ 381, x =>  C0381 = x },
				{ 382, x =>  C0382 = x },
				{ 383, x =>  C0383 = x },
				{ 384, x =>  C0384 = x },
				{ 385, x =>  C0385 = x },
				{ 386, x =>  C0386 = x },
				{ 387, x =>  C0387 = x },
				{ 388, x =>  C0388 = x },
				{ 389, x =>  C0389 = x },
				{ 390, x =>  C0390 = x },
				{ 391, x =>  C0391 = x },
				{ 392, x =>  C0392 = x },
				{ 393, x =>  C0393 = x },
				{ 394, x =>  C0394 = x },
				{ 395, x =>  C0395 = x },
				{ 396, x =>  C0396 = x },
				{ 397, x =>  C0397 = x },
				{ 398, x =>  C0398 = x },
				{ 399, x =>  C0399 = x },
				{ 400, x =>  C0400 = x },
				{ 401, x =>  C0401 = x },
				{ 402, x =>  C0402 = x },
				{ 403, x =>  C0403 = x },
				{ 404, x =>  C0404 = x },
				{ 405, x =>  C0405 = x },
				{ 406, x =>  C0406 = x },
				{ 407, x =>  C0407 = x },
				{ 408, x =>  C0408 = x },
				{ 409, x =>  C0409 = x },
				{ 410, x =>  C0410 = x },
				{ 411, x =>  C0411 = x },
				{ 412, x =>  C0412 = x },
				{ 413, x =>  C0413 = x },
				{ 414, x =>  C0414 = x },
				{ 415, x =>  C0415 = x },
				{ 416, x =>  C0416 = x },
				{ 417, x =>  C0417 = x },
				{ 418, x =>  C0418 = x },
				{ 419, x =>  C0419 = x },
				{ 420, x =>  C0420 = x },
				{ 421, x =>  C0421 = x },
				{ 422, x =>  C0422 = x },
				{ 423, x =>  C0423 = x },
				{ 424, x =>  C0424 = x },
				{ 425, x =>  C0425 = x },
				{ 426, x =>  C0426 = x },
				{ 427, x =>  C0427 = x },
				{ 428, x =>  C0428 = x },
				{ 429, x =>  C0429 = x },
				{ 430, x =>  C0430 = x },
				{ 431, x =>  C0431 = x },
				{ 432, x =>  C0432 = x },
				{ 433, x =>  C0433 = x },
				{ 434, x =>  C0434 = x },
				{ 435, x =>  C0435 = x },
				{ 436, x =>  C0436 = x },
				{ 437, x =>  C0437 = x },
				{ 438, x =>  C0438 = x },
				{ 439, x =>  C0439 = x },
				{ 440, x =>  C0440 = x },
				{ 441, x =>  C0441 = x },
				{ 442, x =>  C0442 = x },
				{ 443, x =>  C0443 = x },
				{ 444, x =>  C0444 = x },
				{ 445, x =>  C0445 = x },
				{ 446, x =>  C0446 = x },
				{ 447, x =>  C0447 = x },
				{ 448, x =>  C0448 = x },
				{ 449, x =>  C0449 = x },
				{ 450, x =>  C0450 = x },
				{ 451, x =>  C0451 = x },
				{ 452, x =>  C0452 = x },
				{ 453, x =>  C0453 = x },
				{ 454, x =>  C0454 = x },
				{ 455, x =>  C0455 = x },
				{ 456, x =>  C0456 = x },
				{ 457, x =>  C0457 = x },
				{ 458, x =>  C0458 = x },
				{ 459, x =>  C0459 = x },
				{ 460, x =>  C0460 = x },
				{ 461, x =>  C0461 = x },
				{ 462, x =>  C0462 = x },
				{ 463, x =>  C0463 = x },
				{ 464, x =>  C0464 = x },
				{ 465, x =>  C0465 = x },
				{ 466, x =>  C0466 = x },
				{ 467, x =>  C0467 = x },
				{ 468, x =>  C0468 = x },
				{ 469, x =>  C0469 = x },
				{ 470, x =>  C0470 = x },
				{ 471, x =>  C0471 = x },
				{ 472, x =>  C0472 = x },
				{ 473, x =>  C0473 = x },
				{ 474, x =>  C0474 = x },
				{ 475, x =>  C0475 = x },
				{ 476, x =>  C0476 = x },
				{ 477, x =>  C0477 = x },
				{ 478, x =>  C0478 = x },
				{ 479, x =>  C0479 = x },
				{ 480, x =>  C0480 = x },
				{ 481, x =>  C0481 = x },
				{ 482, x =>  C0482 = x },
				{ 483, x =>  C0483 = x },
				{ 484, x =>  C0484 = x },
				{ 485, x =>  C0485 = x },
				{ 486, x =>  C0486 = x },
				{ 487, x =>  C0487 = x },
				{ 488, x =>  C0488 = x },
				{ 489, x =>  C0489 = x },
				{ 490, x =>  C0490 = x },
				{ 491, x =>  C0491 = x },
				{ 492, x =>  C0492 = x },
				{ 493, x =>  C0493 = x },
				{ 494, x =>  C0494 = x },
				{ 495, x =>  C0495 = x },
				{ 496, x =>  C0496 = x },
				{ 497, x =>  C0497 = x },
				{ 498, x =>  C0498 = x },
				{ 499, x =>  C0499 = x },
				{ 500, x =>  C0500 = x },
				{ 501, x =>  C0501 = x },
				{ 502, x =>  C0502 = x },
				{ 503, x =>  C0503 = x },
				{ 504, x =>  C0504 = x },
				{ 505, x =>  C0505 = x },
				{ 506, x =>  C0506 = x },
				{ 507, x =>  C0507 = x },
				{ 508, x =>  C0508 = x },
				{ 509, x =>  C0509 = x },
				{ 510, x =>  C0510 = x },
				{ 511, x =>  C0511 = x },
				{ 512, x =>  C0512 = x },
				{ 513, x =>  C0513 = x },
				{ 514, x =>  C0514 = x },
				{ 515, x =>  C0515 = x },
				{ 516, x =>  C0516 = x },
				{ 517, x =>  C0517 = x },
				{ 518, x =>  C0518 = x },
				{ 519, x =>  C0519 = x },
				{ 520, x =>  C0520 = x },
				{ 521, x =>  C0521 = x },
				{ 522, x =>  C0522 = x },
				{ 523, x =>  C0523 = x },
				{ 524, x =>  C0524 = x },
				{ 525, x =>  C0525 = x },
				{ 526, x =>  C0526 = x },
				{ 527, x =>  C0527 = x },
				{ 528, x =>  C0528 = x },
				{ 529, x =>  C0529 = x },
				{ 530, x =>  C0530 = x },
				{ 531, x =>  C0531 = x },
				{ 532, x =>  C0532 = x },
				{ 533, x =>  C0533 = x },
				{ 534, x =>  C0534 = x },
				{ 535, x =>  C0535 = x },
				{ 536, x =>  C0536 = x },
				{ 537, x =>  C0537 = x },
				{ 538, x =>  C0538 = x },
				{ 539, x =>  C0539 = x },
				{ 540, x =>  C0540 = x },
				{ 541, x =>  C0541 = x },
				{ 542, x =>  C0542 = x },
				{ 543, x =>  C0543 = x },
				{ 544, x =>  C0544 = x },
				{ 545, x =>  C0545 = x },
				{ 546, x =>  C0546 = x },
				{ 547, x =>  C0547 = x },
				{ 548, x =>  C0548 = x },
				{ 549, x =>  C0549 = x },
				{ 550, x =>  C0550 = x },
				{ 551, x =>  C0551 = x },
				{ 552, x =>  C0552 = x },
				{ 553, x =>  C0553 = x },
				{ 554, x =>  C0554 = x },
				{ 555, x =>  C0555 = x },
				{ 556, x =>  C0556 = x },
				{ 557, x =>  C0557 = x },
				{ 558, x =>  C0558 = x },
				{ 559, x =>  C0559 = x },
				{ 560, x =>  C0560 = x },
				{ 561, x =>  C0561 = x },
				{ 562, x =>  C0562 = x },
				{ 563, x =>  C0563 = x },
				{ 564, x =>  C0564 = x },
				{ 565, x =>  C0565 = x },
				{ 566, x =>  C0566 = x },
				{ 567, x =>  C0567 = x },
				{ 568, x =>  C0568 = x },
				{ 569, x =>  C0569 = x },
				{ 570, x =>  C0570 = x },
				{ 571, x =>  C0571 = x },
				{ 572, x =>  C0572 = x },
				{ 573, x =>  C0573 = x },
				{ 574, x =>  C0574 = x },
				{ 575, x =>  C0575 = x },
				{ 576, x =>  C0576 = x },
				{ 577, x =>  C0577 = x },
				{ 578, x =>  C0578 = x },
				{ 579, x =>  C0579 = x },
				{ 580, x =>  C0580 = x },
				{ 581, x =>  C0581 = x },
				{ 582, x =>  C0582 = x },
				{ 583, x =>  C0583 = x },
				{ 584, x =>  C0584 = x },
				{ 585, x =>  C0585 = x },
				{ 586, x =>  C0586 = x },
				{ 587, x =>  C0587 = x },
				{ 588, x =>  C0588 = x },
				{ 589, x =>  C0589 = x },
				{ 590, x =>  C0590 = x },
				{ 591, x =>  C0591 = x },
				{ 592, x =>  C0592 = x },
				{ 593, x =>  C0593 = x },
				{ 594, x =>  C0594 = x },
				{ 595, x =>  C0595 = x },
				{ 596, x =>  C0596 = x },
				{ 597, x =>  C0597 = x },
				{ 598, x =>  C0598 = x },
				{ 599, x =>  C0599 = x },
				{ 600, x =>  C0600 = x },
				{ 601, x =>  C0601 = x },
				{ 602, x =>  C0602 = x },
				{ 603, x =>  C0603 = x },
				{ 604, x =>  C0604 = x },
				{ 605, x =>  C0605 = x },
				{ 606, x =>  C0606 = x },
				{ 607, x =>  C0607 = x },
				{ 608, x =>  C0608 = x },
				{ 609, x =>  C0609 = x },
				{ 610, x =>  C0610 = x },
				{ 611, x =>  C0611 = x },
				{ 612, x =>  C0612 = x },
				{ 613, x =>  C0613 = x },
				{ 614, x =>  C0614 = x },
				{ 615, x =>  C0615 = x },
				{ 616, x =>  C0616 = x },
				{ 617, x =>  C0617 = x },
				{ 618, x =>  C0618 = x },
				{ 619, x =>  C0619 = x },
				{ 620, x =>  C0620 = x },
				{ 621, x =>  C0621 = x },
				{ 622, x =>  C0622 = x },
				{ 623, x =>  C0623 = x },
				{ 624, x =>  C0624 = x },
				{ 625, x =>  C0625 = x },
				{ 626, x =>  C0626 = x },
				{ 627, x =>  C0627 = x },
				{ 628, x =>  C0628 = x },
				{ 629, x =>  C0629 = x },
				{ 630, x =>  C0630 = x },
				{ 631, x =>  C0631 = x },
				{ 632, x =>  C0632 = x },
				{ 633, x =>  C0633 = x },
				{ 634, x =>  C0634 = x },
				{ 635, x =>  C0635 = x },
				{ 636, x =>  C0636 = x },
				{ 637, x =>  C0637 = x },
				{ 638, x =>  C0638 = x },
				{ 639, x =>  C0639 = x },
				{ 640, x =>  C0640 = x },
				{ 641, x =>  C0641 = x },
				{ 642, x =>  C0642 = x },
				{ 643, x =>  C0643 = x },
				{ 644, x =>  C0644 = x },
				{ 645, x =>  C0645 = x },
				{ 646, x =>  C0646 = x },
				{ 647, x =>  C0647 = x },
				{ 648, x =>  C0648 = x },
				{ 649, x =>  C0649 = x },
				{ 650, x =>  C0650 = x },
				{ 651, x =>  C0651 = x },
				{ 652, x =>  C0652 = x },
				{ 653, x =>  C0653 = x },
				{ 654, x =>  C0654 = x },
				{ 655, x =>  C0655 = x },
				{ 656, x =>  C0656 = x },
				{ 657, x =>  C0657 = x },
				{ 658, x =>  C0658 = x },
				{ 659, x =>  C0659 = x },
				{ 660, x =>  C0660 = x },
				{ 661, x =>  C0661 = x },
				{ 662, x =>  C0662 = x },
				{ 663, x =>  C0663 = x },
				{ 664, x =>  C0664 = x },
				{ 665, x =>  C0665 = x },
				{ 666, x =>  C0666 = x },
				{ 667, x =>  C0667 = x },
				{ 668, x =>  C0668 = x },
				{ 669, x =>  C0669 = x },
				{ 670, x =>  C0670 = x },
				{ 671, x =>  C0671 = x },
				{ 672, x =>  C0672 = x },
				{ 673, x =>  C0673 = x },
				{ 674, x =>  C0674 = x },
				{ 675, x =>  C0675 = x },
				{ 676, x =>  C0676 = x },
				{ 677, x =>  C0677 = x },
				{ 678, x =>  C0678 = x },
				{ 679, x =>  C0679 = x },
				{ 680, x =>  C0680 = x },
				{ 681, x =>  C0681 = x },
				{ 682, x =>  C0682 = x },
				{ 683, x =>  C0683 = x },
				{ 684, x =>  C0684 = x },
				{ 685, x =>  C0685 = x },
				{ 686, x =>  C0686 = x },
				{ 687, x =>  C0687 = x },
				{ 688, x =>  C0688 = x },
				{ 689, x =>  C0689 = x },
				{ 690, x =>  C0690 = x },
				{ 691, x =>  C0691 = x },
				{ 692, x =>  C0692 = x },
				{ 693, x =>  C0693 = x },
				{ 694, x =>  C0694 = x },
				{ 695, x =>  C0695 = x },
				{ 696, x =>  C0696 = x },
				{ 697, x =>  C0697 = x },
				{ 698, x =>  C0698 = x },
				{ 699, x =>  C0699 = x },
				{ 700, x =>  C0700 = x },
				{ 701, x =>  C0701 = x },
				{ 702, x =>  C0702 = x },
				{ 703, x =>  C0703 = x },
				{ 704, x =>  C0704 = x },
				{ 705, x =>  C0705 = x },
				{ 706, x =>  C0706 = x },
				{ 707, x =>  C0707 = x },
				{ 708, x =>  C0708 = x },
				{ 709, x =>  C0709 = x },
				{ 710, x =>  C0710 = x },
				{ 711, x =>  C0711 = x },
				{ 712, x =>  C0712 = x },
				{ 713, x =>  C0713 = x },
				{ 714, x =>  C0714 = x },
				{ 715, x =>  C0715 = x },
				{ 716, x =>  C0716 = x },
				{ 717, x =>  C0717 = x },
				{ 718, x =>  C0718 = x },
				{ 719, x =>  C0719 = x },
				{ 720, x =>  C0720 = x },
				{ 721, x =>  C0721 = x },
				{ 722, x =>  C0722 = x },
				{ 723, x =>  C0723 = x },
				{ 724, x =>  C0724 = x },
				{ 725, x =>  C0725 = x },
				{ 726, x =>  C0726 = x },
				{ 727, x =>  C0727 = x },
				{ 728, x =>  C0728 = x },
				{ 729, x =>  C0729 = x },
				{ 730, x =>  C0730 = x },
				{ 731, x =>  C0731 = x },
				{ 732, x =>  C0732 = x },
				{ 733, x =>  C0733 = x },
				{ 734, x =>  C0734 = x },
				{ 735, x =>  C0735 = x },
				{ 736, x =>  C0736 = x },
				{ 737, x =>  C0737 = x },
				{ 738, x =>  C0738 = x },
				{ 739, x =>  C0739 = x },
				{ 740, x =>  C0740 = x },
				{ 741, x =>  C0741 = x },
				{ 742, x =>  C0742 = x },
				{ 743, x =>  C0743 = x },
				{ 744, x =>  C0744 = x },
				{ 745, x =>  C0745 = x },
				{ 746, x =>  C0746 = x },
				{ 747, x =>  C0747 = x },
				{ 748, x =>  C0748 = x },
				{ 749, x =>  C0749 = x },
				{ 750, x =>  C0750 = x },
				{ 751, x =>  C0751 = x },
				{ 752, x =>  C0752 = x },
				{ 753, x =>  C0753 = x },
				{ 754, x =>  C0754 = x },
				{ 755, x =>  C0755 = x },
				{ 756, x =>  C0756 = x },
				{ 757, x =>  C0757 = x },
				{ 758, x =>  C0758 = x },
				{ 759, x =>  C0759 = x },
				{ 760, x =>  C0760 = x },
				{ 761, x =>  C0761 = x },
				{ 762, x =>  C0762 = x },
				{ 763, x =>  C0763 = x },
				{ 764, x =>  C0764 = x },
				{ 765, x =>  C0765 = x },
				{ 766, x =>  C0766 = x },
				{ 767, x =>  C0767 = x },
				{ 768, x =>  C0768 = x },
				{ 769, x =>  C0769 = x },
				{ 770, x =>  C0770 = x },
				{ 771, x =>  C0771 = x },
				{ 772, x =>  C0772 = x },
				{ 773, x =>  C0773 = x },
				{ 774, x =>  C0774 = x },
				{ 775, x =>  C0775 = x },
				{ 776, x =>  C0776 = x },
				{ 777, x =>  C0777 = x },
				{ 778, x =>  C0778 = x },
				{ 779, x =>  C0779 = x },
				{ 780, x =>  C0780 = x },
				{ 781, x =>  C0781 = x },
				{ 782, x =>  C0782 = x },
				{ 783, x =>  C0783 = x },
				{ 784, x =>  C0784 = x },
				{ 785, x =>  C0785 = x },
				{ 786, x =>  C0786 = x },
				{ 787, x =>  C0787 = x },
				{ 788, x =>  C0788 = x },
				{ 789, x =>  C0789 = x },
				{ 790, x =>  C0790 = x },
				{ 791, x =>  C0791 = x },
				{ 792, x =>  C0792 = x },
				{ 793, x =>  C0793 = x },
				{ 794, x =>  C0794 = x },
				{ 795, x =>  C0795 = x },
				{ 796, x =>  C0796 = x },
				{ 797, x =>  C0797 = x },
				{ 798, x =>  C0798 = x },
				{ 799, x =>  C0799 = x },
				{ 800, x =>  C0800 = x },
				{ 801, x =>  C0801 = x },
				{ 802, x =>  C0802 = x },
				{ 803, x =>  C0803 = x },
				{ 804, x =>  C0804 = x },
				{ 805, x =>  C0805 = x },
				{ 806, x =>  C0806 = x },
				{ 807, x =>  C0807 = x },
				{ 808, x =>  C0808 = x },
				{ 809, x =>  C0809 = x },
				{ 810, x =>  C0810 = x },
				{ 811, x =>  C0811 = x },
				{ 812, x =>  C0812 = x },
				{ 813, x =>  C0813 = x },
				{ 814, x =>  C0814 = x },
				{ 815, x =>  C0815 = x },
				{ 816, x =>  C0816 = x },
				{ 817, x =>  C0817 = x },
				{ 818, x =>  C0818 = x },
				{ 819, x =>  C0819 = x },
				{ 820, x =>  C0820 = x },
				{ 821, x =>  C0821 = x },
				{ 822, x =>  C0822 = x },
				{ 823, x =>  C0823 = x },
				{ 824, x =>  C0824 = x },
				{ 825, x =>  C0825 = x },
				{ 826, x =>  C0826 = x },
				{ 827, x =>  C0827 = x },
				{ 828, x =>  C0828 = x },
				{ 829, x =>  C0829 = x },
				{ 830, x =>  C0830 = x },
				{ 831, x =>  C0831 = x },
				{ 832, x =>  C0832 = x },
				{ 833, x =>  C0833 = x },
				{ 834, x =>  C0834 = x },
				{ 835, x =>  C0835 = x },
				{ 836, x =>  C0836 = x },
				{ 837, x =>  C0837 = x },
				{ 838, x =>  C0838 = x },
				{ 839, x =>  C0839 = x },
				{ 840, x =>  C0840 = x },
				{ 841, x =>  C0841 = x },
				{ 842, x =>  C0842 = x },
				{ 843, x =>  C0843 = x },
				{ 844, x =>  C0844 = x },
				{ 845, x =>  C0845 = x },
				{ 846, x =>  C0846 = x },
				{ 847, x =>  C0847 = x },
				{ 848, x =>  C0848 = x },
				{ 849, x =>  C0849 = x },
				{ 850, x =>  C0850 = x },
				{ 851, x =>  C0851 = x },
				{ 852, x =>  C0852 = x },
				{ 853, x =>  C0853 = x },
				{ 854, x =>  C0854 = x },
				{ 855, x =>  C0855 = x },
				{ 856, x =>  C0856 = x },
				{ 857, x =>  C0857 = x },
				{ 858, x =>  C0858 = x },
				{ 859, x =>  C0859 = x },
				{ 860, x =>  C0860 = x },
				{ 861, x =>  C0861 = x },
				{ 862, x =>  C0862 = x },
				{ 863, x =>  C0863 = x },
				{ 864, x =>  C0864 = x },
				{ 865, x =>  C0865 = x },
				{ 866, x =>  C0866 = x },
				{ 867, x =>  C0867 = x },
				{ 868, x =>  C0868 = x },
				{ 869, x =>  C0869 = x },
				{ 870, x =>  C0870 = x },
				{ 871, x =>  C0871 = x },
				{ 872, x =>  C0872 = x },
				{ 873, x =>  C0873 = x },
				{ 874, x =>  C0874 = x },
				{ 875, x =>  C0875 = x },
				{ 876, x =>  C0876 = x },
				{ 877, x =>  C0877 = x },
				{ 878, x =>  C0878 = x },
				{ 879, x =>  C0879 = x },
				{ 880, x =>  C0880 = x },
				{ 881, x =>  C0881 = x },
				{ 882, x =>  C0882 = x },
				{ 883, x =>  C0883 = x },
				{ 884, x =>  C0884 = x },
				{ 885, x =>  C0885 = x },
				{ 886, x =>  C0886 = x },
				{ 887, x =>  C0887 = x },
				{ 888, x =>  C0888 = x },
				{ 889, x =>  C0889 = x },
				{ 890, x =>  C0890 = x },
				{ 891, x =>  C0891 = x },
				{ 892, x =>  C0892 = x },
				{ 893, x =>  C0893 = x },
				{ 894, x =>  C0894 = x },
				{ 895, x =>  C0895 = x },
				{ 896, x =>  C0896 = x },
				{ 897, x =>  C0897 = x },
				{ 898, x =>  C0898 = x },
				{ 899, x =>  C0899 = x },
				{ 900, x =>  C0900 = x },
				{ 901, x =>  C0901 = x },
				{ 902, x =>  C0902 = x },
				{ 903, x =>  C0903 = x },
				{ 904, x =>  C0904 = x },
				{ 905, x =>  C0905 = x },
				{ 906, x =>  C0906 = x },
				{ 907, x =>  C0907 = x },
				{ 908, x =>  C0908 = x },
				{ 909, x =>  C0909 = x },
				{ 910, x =>  C0910 = x },
				{ 911, x =>  C0911 = x },
				{ 912, x =>  C0912 = x },
				{ 913, x =>  C0913 = x },
				{ 914, x =>  C0914 = x },
				{ 915, x =>  C0915 = x },
				{ 916, x =>  C0916 = x },
				{ 917, x =>  C0917 = x },
				{ 918, x =>  C0918 = x },
				{ 919, x =>  C0919 = x },
				{ 920, x =>  C0920 = x },
				{ 921, x =>  C0921 = x },
				{ 922, x =>  C0922 = x },
				{ 923, x =>  C0923 = x },
				{ 924, x =>  C0924 = x },
				{ 925, x =>  C0925 = x },
				{ 926, x =>  C0926 = x },
				{ 927, x =>  C0927 = x },
				{ 928, x =>  C0928 = x },
				{ 929, x =>  C0929 = x },
				{ 930, x =>  C0930 = x },
				{ 931, x =>  C0931 = x },
				{ 932, x =>  C0932 = x },
				{ 933, x =>  C0933 = x },
				{ 934, x =>  C0934 = x },
				{ 935, x =>  C0935 = x },
				{ 936, x =>  C0936 = x },
				{ 937, x =>  C0937 = x },
				{ 938, x =>  C0938 = x },
				{ 939, x =>  C0939 = x },
				{ 940, x =>  C0940 = x },
				{ 941, x =>  C0941 = x },
				{ 942, x =>  C0942 = x },
				{ 943, x =>  C0943 = x },
				{ 944, x =>  C0944 = x },
				{ 945, x =>  C0945 = x },
				{ 946, x =>  C0946 = x },
				{ 947, x =>  C0947 = x },
				{ 948, x =>  C0948 = x },
				{ 949, x =>  C0949 = x },
				{ 950, x =>  C0950 = x },
				{ 951, x =>  C0951 = x },
				{ 952, x =>  C0952 = x },
				{ 953, x =>  C0953 = x },
				{ 954, x =>  C0954 = x },
				{ 955, x =>  C0955 = x },
				{ 956, x =>  C0956 = x },
				{ 957, x =>  C0957 = x },
				{ 958, x =>  C0958 = x },
				{ 959, x =>  C0959 = x },
				{ 960, x =>  C0960 = x },
				{ 961, x =>  C0961 = x },
				{ 962, x =>  C0962 = x },
				{ 963, x =>  C0963 = x },
				{ 964, x =>  C0964 = x },
				{ 965, x =>  C0965 = x },
				{ 966, x =>  C0966 = x },
				{ 967, x =>  C0967 = x },
				{ 968, x =>  C0968 = x },
				{ 969, x =>  C0969 = x },
				{ 970, x =>  C0970 = x },
				{ 971, x =>  C0971 = x },
				{ 972, x =>  C0972 = x },
				{ 973, x =>  C0973 = x },
				{ 974, x =>  C0974 = x },
				{ 975, x =>  C0975 = x },
				{ 976, x =>  C0976 = x },
				{ 977, x =>  C0977 = x },
				{ 978, x =>  C0978 = x },
				{ 979, x =>  C0979 = x },
				{ 980, x =>  C0980 = x },
				{ 981, x =>  C0981 = x },
				{ 982, x =>  C0982 = x },
				{ 983, x =>  C0983 = x },
				{ 984, x =>  C0984 = x },
				{ 985, x =>  C0985 = x },
				{ 986, x =>  C0986 = x },
				{ 987, x =>  C0987 = x },
				{ 988, x =>  C0988 = x },
				{ 989, x =>  C0989 = x },
				{ 990, x =>  C0990 = x },
				{ 991, x =>  C0991 = x },
				{ 992, x =>  C0992 = x },
				{ 993, x =>  C0993 = x },
				{ 994, x =>  C0994 = x },
				{ 995, x =>  C0995 = x },
				{ 996, x =>  C0996 = x },
				{ 997, x =>  C0997 = x },
				{ 998, x =>  C0998 = x },
				{ 999, x =>  C0999 = x }
			};
		}

		[LoadColumn(1)]
		public long ﾚｰｽID { get; set; }

		[LoadColumn(2)]
		public float C0000 { get; set; }

		[LoadColumn(3)]
		public float C0001 { get; set; }

		[LoadColumn(4)]
		public float C0002 { get; set; }

		[LoadColumn(5)]
		public float C0003 { get; set; }

		[LoadColumn(6)]
		public float C0004 { get; set; }

		[LoadColumn(7)]
		public float C0005 { get; set; }

		[LoadColumn(8)]
		public float C0006 { get; set; }

		[LoadColumn(9)]
		public float C0007 { get; set; }

		[LoadColumn(10)]
		public float C0008 { get; set; }

		[LoadColumn(11)]
		public float C0009 { get; set; }

		[LoadColumn(12)]
		public float C0010 { get; set; }

		[LoadColumn(13)]
		public float C0011 { get; set; }

		[LoadColumn(14)]
		public float C0012 { get; set; }

		[LoadColumn(15)]
		public float C0013 { get; set; }

		[LoadColumn(16)]
		public float C0014 { get; set; }

		[LoadColumn(17)]
		public float C0015 { get; set; }

		[LoadColumn(18)]
		public float C0016 { get; set; }

		[LoadColumn(19)]
		public float C0017 { get; set; }

		[LoadColumn(20)]
		public float C0018 { get; set; }

		[LoadColumn(21)]
		public float C0019 { get; set; }

		[LoadColumn(22)]
		public float C0020 { get; set; }

		[LoadColumn(23)]
		public float C0021 { get; set; }

		[LoadColumn(24)]
		public float C0022 { get; set; }

		[LoadColumn(25)]
		public float C0023 { get; set; }

		[LoadColumn(26)]
		public float C0024 { get; set; }

		[LoadColumn(27)]
		public float C0025 { get; set; }

		[LoadColumn(28)]
		public float C0026 { get; set; }

		[LoadColumn(29)]
		public float C0027 { get; set; }

		[LoadColumn(30)]
		public float C0028 { get; set; }

		[LoadColumn(31)]
		public float C0029 { get; set; }

		[LoadColumn(32)]
		public float C0030 { get; set; }

		[LoadColumn(33)]
		public float C0031 { get; set; }

		[LoadColumn(34)]
		public float C0032 { get; set; }

		[LoadColumn(35)]
		public float C0033 { get; set; }

		[LoadColumn(36)]
		public float C0034 { get; set; }

		[LoadColumn(37)]
		public float C0035 { get; set; }

		[LoadColumn(38)]
		public float C0036 { get; set; }

		[LoadColumn(39)]
		public float C0037 { get; set; }

		[LoadColumn(40)]
		public float C0038 { get; set; }

		[LoadColumn(41)]
		public float C0039 { get; set; }

		[LoadColumn(42)]
		public float C0040 { get; set; }

		[LoadColumn(43)]
		public float C0041 { get; set; }

		[LoadColumn(44)]
		public float C0042 { get; set; }

		[LoadColumn(45)]
		public float C0043 { get; set; }

		[LoadColumn(46)]
		public float C0044 { get; set; }

		[LoadColumn(47)]
		public float C0045 { get; set; }

		[LoadColumn(48)]
		public float C0046 { get; set; }

		[LoadColumn(49)]
		public float C0047 { get; set; }

		[LoadColumn(50)]
		public float C0048 { get; set; }

		[LoadColumn(51)]
		public float C0049 { get; set; }

		[LoadColumn(52)]
		public float C0050 { get; set; }

		[LoadColumn(53)]
		public float C0051 { get; set; }

		[LoadColumn(54)]
		public float C0052 { get; set; }

		[LoadColumn(55)]
		public float C0053 { get; set; }

		[LoadColumn(56)]
		public float C0054 { get; set; }

		[LoadColumn(57)]
		public float C0055 { get; set; }

		[LoadColumn(58)]
		public float C0056 { get; set; }

		[LoadColumn(59)]
		public float C0057 { get; set; }

		[LoadColumn(60)]
		public float C0058 { get; set; }

		[LoadColumn(61)]
		public float C0059 { get; set; }

		[LoadColumn(62)]
		public float C0060 { get; set; }

		[LoadColumn(63)]
		public float C0061 { get; set; }

		[LoadColumn(64)]
		public float C0062 { get; set; }

		[LoadColumn(65)]
		public float C0063 { get; set; }

		[LoadColumn(66)]
		public float C0064 { get; set; }

		[LoadColumn(67)]
		public float C0065 { get; set; }

		[LoadColumn(68)]
		public float C0066 { get; set; }

		[LoadColumn(69)]
		public float C0067 { get; set; }

		[LoadColumn(70)]
		public float C0068 { get; set; }

		[LoadColumn(71)]
		public float C0069 { get; set; }

		[LoadColumn(72)]
		public float C0070 { get; set; }

		[LoadColumn(73)]
		public float C0071 { get; set; }

		[LoadColumn(74)]
		public float C0072 { get; set; }

		[LoadColumn(75)]
		public float C0073 { get; set; }

		[LoadColumn(76)]
		public float C0074 { get; set; }

		[LoadColumn(77)]
		public float C0075 { get; set; }

		[LoadColumn(78)]
		public float C0076 { get; set; }

		[LoadColumn(79)]
		public float C0077 { get; set; }

		[LoadColumn(80)]
		public float C0078 { get; set; }

		[LoadColumn(81)]
		public float C0079 { get; set; }

		[LoadColumn(82)]
		public float C0080 { get; set; }

		[LoadColumn(83)]
		public float C0081 { get; set; }

		[LoadColumn(84)]
		public float C0082 { get; set; }

		[LoadColumn(85)]
		public float C0083 { get; set; }

		[LoadColumn(86)]
		public float C0084 { get; set; }

		[LoadColumn(87)]
		public float C0085 { get; set; }

		[LoadColumn(88)]
		public float C0086 { get; set; }

		[LoadColumn(89)]
		public float C0087 { get; set; }

		[LoadColumn(90)]
		public float C0088 { get; set; }

		[LoadColumn(91)]
		public float C0089 { get; set; }

		[LoadColumn(92)]
		public float C0090 { get; set; }

		[LoadColumn(93)]
		public float C0091 { get; set; }

		[LoadColumn(94)]
		public float C0092 { get; set; }

		[LoadColumn(95)]
		public float C0093 { get; set; }

		[LoadColumn(96)]
		public float C0094 { get; set; }

		[LoadColumn(97)]
		public float C0095 { get; set; }

		[LoadColumn(98)]
		public float C0096 { get; set; }

		[LoadColumn(99)]
		public float C0097 { get; set; }

		[LoadColumn(100)]
		public float C0098 { get; set; }

		[LoadColumn(101)]
		public float C0099 { get; set; }

		[LoadColumn(102)]
		public float C0100 { get; set; }

		[LoadColumn(103)]
		public float C0101 { get; set; }

		[LoadColumn(104)]
		public float C0102 { get; set; }

		[LoadColumn(105)]
		public float C0103 { get; set; }

		[LoadColumn(106)]
		public float C0104 { get; set; }

		[LoadColumn(107)]
		public float C0105 { get; set; }

		[LoadColumn(108)]
		public float C0106 { get; set; }

		[LoadColumn(109)]
		public float C0107 { get; set; }

		[LoadColumn(110)]
		public float C0108 { get; set; }

		[LoadColumn(111)]
		public float C0109 { get; set; }

		[LoadColumn(112)]
		public float C0110 { get; set; }

		[LoadColumn(113)]
		public float C0111 { get; set; }

		[LoadColumn(114)]
		public float C0112 { get; set; }

		[LoadColumn(115)]
		public float C0113 { get; set; }

		[LoadColumn(116)]
		public float C0114 { get; set; }

		[LoadColumn(117)]
		public float C0115 { get; set; }

		[LoadColumn(118)]
		public float C0116 { get; set; }

		[LoadColumn(119)]
		public float C0117 { get; set; }

		[LoadColumn(120)]
		public float C0118 { get; set; }

		[LoadColumn(121)]
		public float C0119 { get; set; }

		[LoadColumn(122)]
		public float C0120 { get; set; }

		[LoadColumn(123)]
		public float C0121 { get; set; }

		[LoadColumn(124)]
		public float C0122 { get; set; }

		[LoadColumn(125)]
		public float C0123 { get; set; }

		[LoadColumn(126)]
		public float C0124 { get; set; }

		[LoadColumn(127)]
		public float C0125 { get; set; }

		[LoadColumn(128)]
		public float C0126 { get; set; }

		[LoadColumn(129)]
		public float C0127 { get; set; }

		[LoadColumn(130)]
		public float C0128 { get; set; }

		[LoadColumn(131)]
		public float C0129 { get; set; }

		[LoadColumn(132)]
		public float C0130 { get; set; }

		[LoadColumn(133)]
		public float C0131 { get; set; }

		[LoadColumn(134)]
		public float C0132 { get; set; }

		[LoadColumn(135)]
		public float C0133 { get; set; }

		[LoadColumn(136)]
		public float C0134 { get; set; }

		[LoadColumn(137)]
		public float C0135 { get; set; }

		[LoadColumn(138)]
		public float C0136 { get; set; }

		[LoadColumn(139)]
		public float C0137 { get; set; }

		[LoadColumn(140)]
		public float C0138 { get; set; }

		[LoadColumn(141)]
		public float C0139 { get; set; }

		[LoadColumn(142)]
		public float C0140 { get; set; }

		[LoadColumn(143)]
		public float C0141 { get; set; }

		[LoadColumn(144)]
		public float C0142 { get; set; }

		[LoadColumn(145)]
		public float C0143 { get; set; }

		[LoadColumn(146)]
		public float C0144 { get; set; }

		[LoadColumn(147)]
		public float C0145 { get; set; }

		[LoadColumn(148)]
		public float C0146 { get; set; }

		[LoadColumn(149)]
		public float C0147 { get; set; }

		[LoadColumn(150)]
		public float C0148 { get; set; }

		[LoadColumn(151)]
		public float C0149 { get; set; }

		[LoadColumn(152)]
		public float C0150 { get; set; }

		[LoadColumn(153)]
		public float C0151 { get; set; }

		[LoadColumn(154)]
		public float C0152 { get; set; }

		[LoadColumn(155)]
		public float C0153 { get; set; }

		[LoadColumn(156)]
		public float C0154 { get; set; }

		[LoadColumn(157)]
		public float C0155 { get; set; }

		[LoadColumn(158)]
		public float C0156 { get; set; }

		[LoadColumn(159)]
		public float C0157 { get; set; }

		[LoadColumn(160)]
		public float C0158 { get; set; }

		[LoadColumn(161)]
		public float C0159 { get; set; }

		[LoadColumn(162)]
		public float C0160 { get; set; }

		[LoadColumn(163)]
		public float C0161 { get; set; }

		[LoadColumn(164)]
		public float C0162 { get; set; }

		[LoadColumn(165)]
		public float C0163 { get; set; }

		[LoadColumn(166)]
		public float C0164 { get; set; }

		[LoadColumn(167)]
		public float C0165 { get; set; }

		[LoadColumn(168)]
		public float C0166 { get; set; }

		[LoadColumn(169)]
		public float C0167 { get; set; }

		[LoadColumn(170)]
		public float C0168 { get; set; }

		[LoadColumn(171)]
		public float C0169 { get; set; }

		[LoadColumn(172)]
		public float C0170 { get; set; }

		[LoadColumn(173)]
		public float C0171 { get; set; }

		[LoadColumn(174)]
		public float C0172 { get; set; }

		[LoadColumn(175)]
		public float C0173 { get; set; }

		[LoadColumn(176)]
		public float C0174 { get; set; }

		[LoadColumn(177)]
		public float C0175 { get; set; }

		[LoadColumn(178)]
		public float C0176 { get; set; }

		[LoadColumn(179)]
		public float C0177 { get; set; }

		[LoadColumn(180)]
		public float C0178 { get; set; }

		[LoadColumn(181)]
		public float C0179 { get; set; }

		[LoadColumn(182)]
		public float C0180 { get; set; }

		[LoadColumn(183)]
		public float C0181 { get; set; }

		[LoadColumn(184)]
		public float C0182 { get; set; }

		[LoadColumn(185)]
		public float C0183 { get; set; }

		[LoadColumn(186)]
		public float C0184 { get; set; }

		[LoadColumn(187)]
		public float C0185 { get; set; }

		[LoadColumn(188)]
		public float C0186 { get; set; }

		[LoadColumn(189)]
		public float C0187 { get; set; }

		[LoadColumn(190)]
		public float C0188 { get; set; }

		[LoadColumn(191)]
		public float C0189 { get; set; }

		[LoadColumn(192)]
		public float C0190 { get; set; }

		[LoadColumn(193)]
		public float C0191 { get; set; }

		[LoadColumn(194)]
		public float C0192 { get; set; }

		[LoadColumn(195)]
		public float C0193 { get; set; }

		[LoadColumn(196)]
		public float C0194 { get; set; }

		[LoadColumn(197)]
		public float C0195 { get; set; }

		[LoadColumn(198)]
		public float C0196 { get; set; }

		[LoadColumn(199)]
		public float C0197 { get; set; }

		[LoadColumn(200)]
		public float C0198 { get; set; }

		[LoadColumn(201)]
		public float C0199 { get; set; }

		[LoadColumn(202)]
		public float C0200 { get; set; }

		[LoadColumn(203)]
		public float C0201 { get; set; }

		[LoadColumn(204)]
		public float C0202 { get; set; }

		[LoadColumn(205)]
		public float C0203 { get; set; }

		[LoadColumn(206)]
		public float C0204 { get; set; }

		[LoadColumn(207)]
		public float C0205 { get; set; }

		[LoadColumn(208)]
		public float C0206 { get; set; }

		[LoadColumn(209)]
		public float C0207 { get; set; }

		[LoadColumn(210)]
		public float C0208 { get; set; }

		[LoadColumn(211)]
		public float C0209 { get; set; }

		[LoadColumn(212)]
		public float C0210 { get; set; }

		[LoadColumn(213)]
		public float C0211 { get; set; }

		[LoadColumn(214)]
		public float C0212 { get; set; }

		[LoadColumn(215)]
		public float C0213 { get; set; }

		[LoadColumn(216)]
		public float C0214 { get; set; }

		[LoadColumn(217)]
		public float C0215 { get; set; }

		[LoadColumn(218)]
		public float C0216 { get; set; }

		[LoadColumn(219)]
		public float C0217 { get; set; }

		[LoadColumn(220)]
		public float C0218 { get; set; }

		[LoadColumn(221)]
		public float C0219 { get; set; }

		[LoadColumn(222)]
		public float C0220 { get; set; }

		[LoadColumn(223)]
		public float C0221 { get; set; }

		[LoadColumn(224)]
		public float C0222 { get; set; }

		[LoadColumn(225)]
		public float C0223 { get; set; }

		[LoadColumn(226)]
		public float C0224 { get; set; }

		[LoadColumn(227)]
		public float C0225 { get; set; }

		[LoadColumn(228)]
		public float C0226 { get; set; }

		[LoadColumn(229)]
		public float C0227 { get; set; }

		[LoadColumn(230)]
		public float C0228 { get; set; }

		[LoadColumn(231)]
		public float C0229 { get; set; }

		[LoadColumn(232)]
		public float C0230 { get; set; }

		[LoadColumn(233)]
		public float C0231 { get; set; }

		[LoadColumn(234)]
		public float C0232 { get; set; }

		[LoadColumn(235)]
		public float C0233 { get; set; }

		[LoadColumn(236)]
		public float C0234 { get; set; }

		[LoadColumn(237)]
		public float C0235 { get; set; }

		[LoadColumn(238)]
		public float C0236 { get; set; }

		[LoadColumn(239)]
		public float C0237 { get; set; }

		[LoadColumn(240)]
		public float C0238 { get; set; }

		[LoadColumn(241)]
		public float C0239 { get; set; }

		[LoadColumn(242)]
		public float C0240 { get; set; }

		[LoadColumn(243)]
		public float C0241 { get; set; }

		[LoadColumn(244)]
		public float C0242 { get; set; }

		[LoadColumn(245)]
		public float C0243 { get; set; }

		[LoadColumn(246)]
		public float C0244 { get; set; }

		[LoadColumn(247)]
		public float C0245 { get; set; }

		[LoadColumn(248)]
		public float C0246 { get; set; }

		[LoadColumn(249)]
		public float C0247 { get; set; }

		[LoadColumn(250)]
		public float C0248 { get; set; }

		[LoadColumn(251)]
		public float C0249 { get; set; }

		[LoadColumn(252)]
		public float C0250 { get; set; }

		[LoadColumn(253)]
		public float C0251 { get; set; }

		[LoadColumn(254)]
		public float C0252 { get; set; }

		[LoadColumn(255)]
		public float C0253 { get; set; }

		[LoadColumn(256)]
		public float C0254 { get; set; }

		[LoadColumn(257)]
		public float C0255 { get; set; }

		[LoadColumn(258)]
		public float C0256 { get; set; }

		[LoadColumn(259)]
		public float C0257 { get; set; }

		[LoadColumn(260)]
		public float C0258 { get; set; }

		[LoadColumn(261)]
		public float C0259 { get; set; }

		[LoadColumn(262)]
		public float C0260 { get; set; }

		[LoadColumn(263)]
		public float C0261 { get; set; }

		[LoadColumn(264)]
		public float C0262 { get; set; }

		[LoadColumn(265)]
		public float C0263 { get; set; }

		[LoadColumn(266)]
		public float C0264 { get; set; }

		[LoadColumn(267)]
		public float C0265 { get; set; }

		[LoadColumn(268)]
		public float C0266 { get; set; }

		[LoadColumn(269)]
		public float C0267 { get; set; }

		[LoadColumn(270)]
		public float C0268 { get; set; }

		[LoadColumn(271)]
		public float C0269 { get; set; }

		[LoadColumn(272)]
		public float C0270 { get; set; }

		[LoadColumn(273)]
		public float C0271 { get; set; }

		[LoadColumn(274)]
		public float C0272 { get; set; }

		[LoadColumn(275)]
		public float C0273 { get; set; }

		[LoadColumn(276)]
		public float C0274 { get; set; }

		[LoadColumn(277)]
		public float C0275 { get; set; }

		[LoadColumn(278)]
		public float C0276 { get; set; }

		[LoadColumn(279)]
		public float C0277 { get; set; }

		[LoadColumn(280)]
		public float C0278 { get; set; }

		[LoadColumn(281)]
		public float C0279 { get; set; }

		[LoadColumn(282)]
		public float C0280 { get; set; }

		[LoadColumn(283)]
		public float C0281 { get; set; }

		[LoadColumn(284)]
		public float C0282 { get; set; }

		[LoadColumn(285)]
		public float C0283 { get; set; }

		[LoadColumn(286)]
		public float C0284 { get; set; }

		[LoadColumn(287)]
		public float C0285 { get; set; }

		[LoadColumn(288)]
		public float C0286 { get; set; }

		[LoadColumn(289)]
		public float C0287 { get; set; }

		[LoadColumn(290)]
		public float C0288 { get; set; }

		[LoadColumn(291)]
		public float C0289 { get; set; }

		[LoadColumn(292)]
		public float C0290 { get; set; }

		[LoadColumn(293)]
		public float C0291 { get; set; }

		[LoadColumn(294)]
		public float C0292 { get; set; }

		[LoadColumn(295)]
		public float C0293 { get; set; }

		[LoadColumn(296)]
		public float C0294 { get; set; }

		[LoadColumn(297)]
		public float C0295 { get; set; }

		[LoadColumn(298)]
		public float C0296 { get; set; }

		[LoadColumn(299)]
		public float C0297 { get; set; }

		[LoadColumn(300)]
		public float C0298 { get; set; }

		[LoadColumn(301)]
		public float C0299 { get; set; }

		[LoadColumn(302)]
		public float C0300 { get; set; }

		[LoadColumn(303)]
		public float C0301 { get; set; }

		[LoadColumn(304)]
		public float C0302 { get; set; }

		[LoadColumn(305)]
		public float C0303 { get; set; }

		[LoadColumn(306)]
		public float C0304 { get; set; }

		[LoadColumn(307)]
		public float C0305 { get; set; }

		[LoadColumn(308)]
		public float C0306 { get; set; }

		[LoadColumn(309)]
		public float C0307 { get; set; }

		[LoadColumn(310)]
		public float C0308 { get; set; }

		[LoadColumn(311)]
		public float C0309 { get; set; }

		[LoadColumn(312)]
		public float C0310 { get; set; }

		[LoadColumn(313)]
		public float C0311 { get; set; }

		[LoadColumn(314)]
		public float C0312 { get; set; }

		[LoadColumn(315)]
		public float C0313 { get; set; }

		[LoadColumn(316)]
		public float C0314 { get; set; }

		[LoadColumn(317)]
		public float C0315 { get; set; }

		[LoadColumn(318)]
		public float C0316 { get; set; }

		[LoadColumn(319)]
		public float C0317 { get; set; }

		[LoadColumn(320)]
		public float C0318 { get; set; }

		[LoadColumn(321)]
		public float C0319 { get; set; }

		[LoadColumn(322)]
		public float C0320 { get; set; }

		[LoadColumn(323)]
		public float C0321 { get; set; }

		[LoadColumn(324)]
		public float C0322 { get; set; }

		[LoadColumn(325)]
		public float C0323 { get; set; }

		[LoadColumn(326)]
		public float C0324 { get; set; }

		[LoadColumn(327)]
		public float C0325 { get; set; }

		[LoadColumn(328)]
		public float C0326 { get; set; }

		[LoadColumn(329)]
		public float C0327 { get; set; }

		[LoadColumn(330)]
		public float C0328 { get; set; }

		[LoadColumn(331)]
		public float C0329 { get; set; }

		[LoadColumn(332)]
		public float C0330 { get; set; }

		[LoadColumn(333)]
		public float C0331 { get; set; }

		[LoadColumn(334)]
		public float C0332 { get; set; }

		[LoadColumn(335)]
		public float C0333 { get; set; }

		[LoadColumn(336)]
		public float C0334 { get; set; }

		[LoadColumn(337)]
		public float C0335 { get; set; }

		[LoadColumn(338)]
		public float C0336 { get; set; }

		[LoadColumn(339)]
		public float C0337 { get; set; }

		[LoadColumn(340)]
		public float C0338 { get; set; }

		[LoadColumn(341)]
		public float C0339 { get; set; }

		[LoadColumn(342)]
		public float C0340 { get; set; }

		[LoadColumn(343)]
		public float C0341 { get; set; }

		[LoadColumn(344)]
		public float C0342 { get; set; }

		[LoadColumn(345)]
		public float C0343 { get; set; }

		[LoadColumn(346)]
		public float C0344 { get; set; }

		[LoadColumn(347)]
		public float C0345 { get; set; }

		[LoadColumn(348)]
		public float C0346 { get; set; }

		[LoadColumn(349)]
		public float C0347 { get; set; }

		[LoadColumn(350)]
		public float C0348 { get; set; }

		[LoadColumn(351)]
		public float C0349 { get; set; }

		[LoadColumn(352)]
		public float C0350 { get; set; }

		[LoadColumn(353)]
		public float C0351 { get; set; }

		[LoadColumn(354)]
		public float C0352 { get; set; }

		[LoadColumn(355)]
		public float C0353 { get; set; }

		[LoadColumn(356)]
		public float C0354 { get; set; }

		[LoadColumn(357)]
		public float C0355 { get; set; }

		[LoadColumn(358)]
		public float C0356 { get; set; }

		[LoadColumn(359)]
		public float C0357 { get; set; }

		[LoadColumn(360)]
		public float C0358 { get; set; }

		[LoadColumn(361)]
		public float C0359 { get; set; }

		[LoadColumn(362)]
		public float C0360 { get; set; }

		[LoadColumn(363)]
		public float C0361 { get; set; }

		[LoadColumn(364)]
		public float C0362 { get; set; }

		[LoadColumn(365)]
		public float C0363 { get; set; }

		[LoadColumn(366)]
		public float C0364 { get; set; }

		[LoadColumn(367)]
		public float C0365 { get; set; }

		[LoadColumn(368)]
		public float C0366 { get; set; }

		[LoadColumn(369)]
		public float C0367 { get; set; }

		[LoadColumn(370)]
		public float C0368 { get; set; }

		[LoadColumn(371)]
		public float C0369 { get; set; }

		[LoadColumn(372)]
		public float C0370 { get; set; }

		[LoadColumn(373)]
		public float C0371 { get; set; }

		[LoadColumn(374)]
		public float C0372 { get; set; }

		[LoadColumn(375)]
		public float C0373 { get; set; }

		[LoadColumn(376)]
		public float C0374 { get; set; }

		[LoadColumn(377)]
		public float C0375 { get; set; }

		[LoadColumn(378)]
		public float C0376 { get; set; }

		[LoadColumn(379)]
		public float C0377 { get; set; }

		[LoadColumn(380)]
		public float C0378 { get; set; }

		[LoadColumn(381)]
		public float C0379 { get; set; }

		[LoadColumn(382)]
		public float C0380 { get; set; }

		[LoadColumn(383)]
		public float C0381 { get; set; }

		[LoadColumn(384)]
		public float C0382 { get; set; }

		[LoadColumn(385)]
		public float C0383 { get; set; }

		[LoadColumn(386)]
		public float C0384 { get; set; }

		[LoadColumn(387)]
		public float C0385 { get; set; }

		[LoadColumn(388)]
		public float C0386 { get; set; }

		[LoadColumn(389)]
		public float C0387 { get; set; }

		[LoadColumn(390)]
		public float C0388 { get; set; }

		[LoadColumn(391)]
		public float C0389 { get; set; }

		[LoadColumn(392)]
		public float C0390 { get; set; }

		[LoadColumn(393)]
		public float C0391 { get; set; }

		[LoadColumn(394)]
		public float C0392 { get; set; }

		[LoadColumn(395)]
		public float C0393 { get; set; }

		[LoadColumn(396)]
		public float C0394 { get; set; }

		[LoadColumn(397)]
		public float C0395 { get; set; }

		[LoadColumn(398)]
		public float C0396 { get; set; }

		[LoadColumn(399)]
		public float C0397 { get; set; }

		[LoadColumn(400)]
		public float C0398 { get; set; }

		[LoadColumn(401)]
		public float C0399 { get; set; }

		[LoadColumn(402)]
		public float C0400 { get; set; }

		[LoadColumn(403)]
		public float C0401 { get; set; }

		[LoadColumn(404)]
		public float C0402 { get; set; }

		[LoadColumn(405)]
		public float C0403 { get; set; }

		[LoadColumn(406)]
		public float C0404 { get; set; }

		[LoadColumn(407)]
		public float C0405 { get; set; }

		[LoadColumn(408)]
		public float C0406 { get; set; }

		[LoadColumn(409)]
		public float C0407 { get; set; }

		[LoadColumn(410)]
		public float C0408 { get; set; }

		[LoadColumn(411)]
		public float C0409 { get; set; }

		[LoadColumn(412)]
		public float C0410 { get; set; }

		[LoadColumn(413)]
		public float C0411 { get; set; }

		[LoadColumn(414)]
		public float C0412 { get; set; }

		[LoadColumn(415)]
		public float C0413 { get; set; }

		[LoadColumn(416)]
		public float C0414 { get; set; }

		[LoadColumn(417)]
		public float C0415 { get; set; }

		[LoadColumn(418)]
		public float C0416 { get; set; }

		[LoadColumn(419)]
		public float C0417 { get; set; }

		[LoadColumn(420)]
		public float C0418 { get; set; }

		[LoadColumn(421)]
		public float C0419 { get; set; }

		[LoadColumn(422)]
		public float C0420 { get; set; }

		[LoadColumn(423)]
		public float C0421 { get; set; }

		[LoadColumn(424)]
		public float C0422 { get; set; }

		[LoadColumn(425)]
		public float C0423 { get; set; }

		[LoadColumn(426)]
		public float C0424 { get; set; }

		[LoadColumn(427)]
		public float C0425 { get; set; }

		[LoadColumn(428)]
		public float C0426 { get; set; }

		[LoadColumn(429)]
		public float C0427 { get; set; }

		[LoadColumn(430)]
		public float C0428 { get; set; }

		[LoadColumn(431)]
		public float C0429 { get; set; }

		[LoadColumn(432)]
		public float C0430 { get; set; }

		[LoadColumn(433)]
		public float C0431 { get; set; }

		[LoadColumn(434)]
		public float C0432 { get; set; }

		[LoadColumn(435)]
		public float C0433 { get; set; }

		[LoadColumn(436)]
		public float C0434 { get; set; }

		[LoadColumn(437)]
		public float C0435 { get; set; }

		[LoadColumn(438)]
		public float C0436 { get; set; }

		[LoadColumn(439)]
		public float C0437 { get; set; }

		[LoadColumn(440)]
		public float C0438 { get; set; }

		[LoadColumn(441)]
		public float C0439 { get; set; }

		[LoadColumn(442)]
		public float C0440 { get; set; }

		[LoadColumn(443)]
		public float C0441 { get; set; }

		[LoadColumn(444)]
		public float C0442 { get; set; }

		[LoadColumn(445)]
		public float C0443 { get; set; }

		[LoadColumn(446)]
		public float C0444 { get; set; }

		[LoadColumn(447)]
		public float C0445 { get; set; }

		[LoadColumn(448)]
		public float C0446 { get; set; }

		[LoadColumn(449)]
		public float C0447 { get; set; }

		[LoadColumn(450)]
		public float C0448 { get; set; }

		[LoadColumn(451)]
		public float C0449 { get; set; }

		[LoadColumn(452)]
		public float C0450 { get; set; }

		[LoadColumn(453)]
		public float C0451 { get; set; }

		[LoadColumn(454)]
		public float C0452 { get; set; }

		[LoadColumn(455)]
		public float C0453 { get; set; }

		[LoadColumn(456)]
		public float C0454 { get; set; }

		[LoadColumn(457)]
		public float C0455 { get; set; }

		[LoadColumn(458)]
		public float C0456 { get; set; }

		[LoadColumn(459)]
		public float C0457 { get; set; }

		[LoadColumn(460)]
		public float C0458 { get; set; }

		[LoadColumn(461)]
		public float C0459 { get; set; }

		[LoadColumn(462)]
		public float C0460 { get; set; }

		[LoadColumn(463)]
		public float C0461 { get; set; }

		[LoadColumn(464)]
		public float C0462 { get; set; }

		[LoadColumn(465)]
		public float C0463 { get; set; }

		[LoadColumn(466)]
		public float C0464 { get; set; }

		[LoadColumn(467)]
		public float C0465 { get; set; }

		[LoadColumn(468)]
		public float C0466 { get; set; }

		[LoadColumn(469)]
		public float C0467 { get; set; }

		[LoadColumn(470)]
		public float C0468 { get; set; }

		[LoadColumn(471)]
		public float C0469 { get; set; }

		[LoadColumn(472)]
		public float C0470 { get; set; }

		[LoadColumn(473)]
		public float C0471 { get; set; }

		[LoadColumn(474)]
		public float C0472 { get; set; }

		[LoadColumn(475)]
		public float C0473 { get; set; }

		[LoadColumn(476)]
		public float C0474 { get; set; }

		[LoadColumn(477)]
		public float C0475 { get; set; }

		[LoadColumn(478)]
		public float C0476 { get; set; }

		[LoadColumn(479)]
		public float C0477 { get; set; }

		[LoadColumn(480)]
		public float C0478 { get; set; }

		[LoadColumn(481)]
		public float C0479 { get; set; }

		[LoadColumn(482)]
		public float C0480 { get; set; }

		[LoadColumn(483)]
		public float C0481 { get; set; }

		[LoadColumn(484)]
		public float C0482 { get; set; }

		[LoadColumn(485)]
		public float C0483 { get; set; }

		[LoadColumn(486)]
		public float C0484 { get; set; }

		[LoadColumn(487)]
		public float C0485 { get; set; }

		[LoadColumn(488)]
		public float C0486 { get; set; }

		[LoadColumn(489)]
		public float C0487 { get; set; }

		[LoadColumn(490)]
		public float C0488 { get; set; }

		[LoadColumn(491)]
		public float C0489 { get; set; }

		[LoadColumn(492)]
		public float C0490 { get; set; }

		[LoadColumn(493)]
		public float C0491 { get; set; }

		[LoadColumn(494)]
		public float C0492 { get; set; }

		[LoadColumn(495)]
		public float C0493 { get; set; }

		[LoadColumn(496)]
		public float C0494 { get; set; }

		[LoadColumn(497)]
		public float C0495 { get; set; }

		[LoadColumn(498)]
		public float C0496 { get; set; }

		[LoadColumn(499)]
		public float C0497 { get; set; }

		[LoadColumn(500)]
		public float C0498 { get; set; }

		[LoadColumn(501)]
		public float C0499 { get; set; }

		[LoadColumn(502)]
		public float C0500 { get; set; }

		[LoadColumn(503)]
		public float C0501 { get; set; }

		[LoadColumn(504)]
		public float C0502 { get; set; }

		[LoadColumn(505)]
		public float C0503 { get; set; }

		[LoadColumn(506)]
		public float C0504 { get; set; }

		[LoadColumn(507)]
		public float C0505 { get; set; }

		[LoadColumn(508)]
		public float C0506 { get; set; }

		[LoadColumn(509)]
		public float C0507 { get; set; }

		[LoadColumn(510)]
		public float C0508 { get; set; }

		[LoadColumn(511)]
		public float C0509 { get; set; }

		[LoadColumn(512)]
		public float C0510 { get; set; }

		[LoadColumn(513)]
		public float C0511 { get; set; }

		[LoadColumn(514)]
		public float C0512 { get; set; }

		[LoadColumn(515)]
		public float C0513 { get; set; }

		[LoadColumn(516)]
		public float C0514 { get; set; }

		[LoadColumn(517)]
		public float C0515 { get; set; }

		[LoadColumn(518)]
		public float C0516 { get; set; }

		[LoadColumn(519)]
		public float C0517 { get; set; }

		[LoadColumn(520)]
		public float C0518 { get; set; }

		[LoadColumn(521)]
		public float C0519 { get; set; }

		[LoadColumn(522)]
		public float C0520 { get; set; }

		[LoadColumn(523)]
		public float C0521 { get; set; }

		[LoadColumn(524)]
		public float C0522 { get; set; }

		[LoadColumn(525)]
		public float C0523 { get; set; }

		[LoadColumn(526)]
		public float C0524 { get; set; }

		[LoadColumn(527)]
		public float C0525 { get; set; }

		[LoadColumn(528)]
		public float C0526 { get; set; }

		[LoadColumn(529)]
		public float C0527 { get; set; }

		[LoadColumn(530)]
		public float C0528 { get; set; }

		[LoadColumn(531)]
		public float C0529 { get; set; }

		[LoadColumn(532)]
		public float C0530 { get; set; }

		[LoadColumn(533)]
		public float C0531 { get; set; }

		[LoadColumn(534)]
		public float C0532 { get; set; }

		[LoadColumn(535)]
		public float C0533 { get; set; }

		[LoadColumn(536)]
		public float C0534 { get; set; }

		[LoadColumn(537)]
		public float C0535 { get; set; }

		[LoadColumn(538)]
		public float C0536 { get; set; }

		[LoadColumn(539)]
		public float C0537 { get; set; }

		[LoadColumn(540)]
		public float C0538 { get; set; }

		[LoadColumn(541)]
		public float C0539 { get; set; }

		[LoadColumn(542)]
		public float C0540 { get; set; }

		[LoadColumn(543)]
		public float C0541 { get; set; }

		[LoadColumn(544)]
		public float C0542 { get; set; }

		[LoadColumn(545)]
		public float C0543 { get; set; }

		[LoadColumn(546)]
		public float C0544 { get; set; }

		[LoadColumn(547)]
		public float C0545 { get; set; }

		[LoadColumn(548)]
		public float C0546 { get; set; }

		[LoadColumn(549)]
		public float C0547 { get; set; }

		[LoadColumn(550)]
		public float C0548 { get; set; }

		[LoadColumn(551)]
		public float C0549 { get; set; }

		[LoadColumn(552)]
		public float C0550 { get; set; }

		[LoadColumn(553)]
		public float C0551 { get; set; }

		[LoadColumn(554)]
		public float C0552 { get; set; }

		[LoadColumn(555)]
		public float C0553 { get; set; }

		[LoadColumn(556)]
		public float C0554 { get; set; }

		[LoadColumn(557)]
		public float C0555 { get; set; }

		[LoadColumn(558)]
		public float C0556 { get; set; }

		[LoadColumn(559)]
		public float C0557 { get; set; }

		[LoadColumn(560)]
		public float C0558 { get; set; }

		[LoadColumn(561)]
		public float C0559 { get; set; }

		[LoadColumn(562)]
		public float C0560 { get; set; }

		[LoadColumn(563)]
		public float C0561 { get; set; }

		[LoadColumn(564)]
		public float C0562 { get; set; }

		[LoadColumn(565)]
		public float C0563 { get; set; }

		[LoadColumn(566)]
		public float C0564 { get; set; }

		[LoadColumn(567)]
		public float C0565 { get; set; }

		[LoadColumn(568)]
		public float C0566 { get; set; }

		[LoadColumn(569)]
		public float C0567 { get; set; }

		[LoadColumn(570)]
		public float C0568 { get; set; }

		[LoadColumn(571)]
		public float C0569 { get; set; }

		[LoadColumn(572)]
		public float C0570 { get; set; }

		[LoadColumn(573)]
		public float C0571 { get; set; }

		[LoadColumn(574)]
		public float C0572 { get; set; }

		[LoadColumn(575)]
		public float C0573 { get; set; }

		[LoadColumn(576)]
		public float C0574 { get; set; }

		[LoadColumn(577)]
		public float C0575 { get; set; }

		[LoadColumn(578)]
		public float C0576 { get; set; }

		[LoadColumn(579)]
		public float C0577 { get; set; }

		[LoadColumn(580)]
		public float C0578 { get; set; }

		[LoadColumn(581)]
		public float C0579 { get; set; }

		[LoadColumn(582)]
		public float C0580 { get; set; }

		[LoadColumn(583)]
		public float C0581 { get; set; }

		[LoadColumn(584)]
		public float C0582 { get; set; }

		[LoadColumn(585)]
		public float C0583 { get; set; }

		[LoadColumn(586)]
		public float C0584 { get; set; }

		[LoadColumn(587)]
		public float C0585 { get; set; }

		[LoadColumn(588)]
		public float C0586 { get; set; }

		[LoadColumn(589)]
		public float C0587 { get; set; }

		[LoadColumn(590)]
		public float C0588 { get; set; }

		[LoadColumn(591)]
		public float C0589 { get; set; }

		[LoadColumn(592)]
		public float C0590 { get; set; }

		[LoadColumn(593)]
		public float C0591 { get; set; }

		[LoadColumn(594)]
		public float C0592 { get; set; }

		[LoadColumn(595)]
		public float C0593 { get; set; }

		[LoadColumn(596)]
		public float C0594 { get; set; }

		[LoadColumn(597)]
		public float C0595 { get; set; }

		[LoadColumn(598)]
		public float C0596 { get; set; }

		[LoadColumn(599)]
		public float C0597 { get; set; }

		[LoadColumn(600)]
		public float C0598 { get; set; }

		[LoadColumn(601)]
		public float C0599 { get; set; }

		[LoadColumn(602)]
		public float C0600 { get; set; }

		[LoadColumn(603)]
		public float C0601 { get; set; }

		[LoadColumn(604)]
		public float C0602 { get; set; }

		[LoadColumn(605)]
		public float C0603 { get; set; }

		[LoadColumn(606)]
		public float C0604 { get; set; }

		[LoadColumn(607)]
		public float C0605 { get; set; }

		[LoadColumn(608)]
		public float C0606 { get; set; }

		[LoadColumn(609)]
		public float C0607 { get; set; }

		[LoadColumn(610)]
		public float C0608 { get; set; }

		[LoadColumn(611)]
		public float C0609 { get; set; }

		[LoadColumn(612)]
		public float C0610 { get; set; }

		[LoadColumn(613)]
		public float C0611 { get; set; }

		[LoadColumn(614)]
		public float C0612 { get; set; }

		[LoadColumn(615)]
		public float C0613 { get; set; }

		[LoadColumn(616)]
		public float C0614 { get; set; }

		[LoadColumn(617)]
		public float C0615 { get; set; }

		[LoadColumn(618)]
		public float C0616 { get; set; }

		[LoadColumn(619)]
		public float C0617 { get; set; }

		[LoadColumn(620)]
		public float C0618 { get; set; }

		[LoadColumn(621)]
		public float C0619 { get; set; }

		[LoadColumn(622)]
		public float C0620 { get; set; }

		[LoadColumn(623)]
		public float C0621 { get; set; }

		[LoadColumn(624)]
		public float C0622 { get; set; }

		[LoadColumn(625)]
		public float C0623 { get; set; }

		[LoadColumn(626)]
		public float C0624 { get; set; }

		[LoadColumn(627)]
		public float C0625 { get; set; }

		[LoadColumn(628)]
		public float C0626 { get; set; }

		[LoadColumn(629)]
		public float C0627 { get; set; }

		[LoadColumn(630)]
		public float C0628 { get; set; }

		[LoadColumn(631)]
		public float C0629 { get; set; }

		[LoadColumn(632)]
		public float C0630 { get; set; }

		[LoadColumn(633)]
		public float C0631 { get; set; }

		[LoadColumn(634)]
		public float C0632 { get; set; }

		[LoadColumn(635)]
		public float C0633 { get; set; }

		[LoadColumn(636)]
		public float C0634 { get; set; }

		[LoadColumn(637)]
		public float C0635 { get; set; }

		[LoadColumn(638)]
		public float C0636 { get; set; }

		[LoadColumn(639)]
		public float C0637 { get; set; }

		[LoadColumn(640)]
		public float C0638 { get; set; }

		[LoadColumn(641)]
		public float C0639 { get; set; }

		[LoadColumn(642)]
		public float C0640 { get; set; }

		[LoadColumn(643)]
		public float C0641 { get; set; }

		[LoadColumn(644)]
		public float C0642 { get; set; }

		[LoadColumn(645)]
		public float C0643 { get; set; }

		[LoadColumn(646)]
		public float C0644 { get; set; }

		[LoadColumn(647)]
		public float C0645 { get; set; }

		[LoadColumn(648)]
		public float C0646 { get; set; }

		[LoadColumn(649)]
		public float C0647 { get; set; }

		[LoadColumn(650)]
		public float C0648 { get; set; }

		[LoadColumn(651)]
		public float C0649 { get; set; }

		[LoadColumn(652)]
		public float C0650 { get; set; }

		[LoadColumn(653)]
		public float C0651 { get; set; }

		[LoadColumn(654)]
		public float C0652 { get; set; }

		[LoadColumn(655)]
		public float C0653 { get; set; }

		[LoadColumn(656)]
		public float C0654 { get; set; }

		[LoadColumn(657)]
		public float C0655 { get; set; }

		[LoadColumn(658)]
		public float C0656 { get; set; }

		[LoadColumn(659)]
		public float C0657 { get; set; }

		[LoadColumn(660)]
		public float C0658 { get; set; }

		[LoadColumn(661)]
		public float C0659 { get; set; }

		[LoadColumn(662)]
		public float C0660 { get; set; }

		[LoadColumn(663)]
		public float C0661 { get; set; }

		[LoadColumn(664)]
		public float C0662 { get; set; }

		[LoadColumn(665)]
		public float C0663 { get; set; }

		[LoadColumn(666)]
		public float C0664 { get; set; }

		[LoadColumn(667)]
		public float C0665 { get; set; }

		[LoadColumn(668)]
		public float C0666 { get; set; }

		[LoadColumn(669)]
		public float C0667 { get; set; }

		[LoadColumn(670)]
		public float C0668 { get; set; }

		[LoadColumn(671)]
		public float C0669 { get; set; }

		[LoadColumn(672)]
		public float C0670 { get; set; }

		[LoadColumn(673)]
		public float C0671 { get; set; }

		[LoadColumn(674)]
		public float C0672 { get; set; }

		[LoadColumn(675)]
		public float C0673 { get; set; }

		[LoadColumn(676)]
		public float C0674 { get; set; }

		[LoadColumn(677)]
		public float C0675 { get; set; }

		[LoadColumn(678)]
		public float C0676 { get; set; }

		[LoadColumn(679)]
		public float C0677 { get; set; }

		[LoadColumn(680)]
		public float C0678 { get; set; }

		[LoadColumn(681)]
		public float C0679 { get; set; }

		[LoadColumn(682)]
		public float C0680 { get; set; }

		[LoadColumn(683)]
		public float C0681 { get; set; }

		[LoadColumn(684)]
		public float C0682 { get; set; }

		[LoadColumn(685)]
		public float C0683 { get; set; }

		[LoadColumn(686)]
		public float C0684 { get; set; }

		[LoadColumn(687)]
		public float C0685 { get; set; }

		[LoadColumn(688)]
		public float C0686 { get; set; }

		[LoadColumn(689)]
		public float C0687 { get; set; }

		[LoadColumn(690)]
		public float C0688 { get; set; }

		[LoadColumn(691)]
		public float C0689 { get; set; }

		[LoadColumn(692)]
		public float C0690 { get; set; }

		[LoadColumn(693)]
		public float C0691 { get; set; }

		[LoadColumn(694)]
		public float C0692 { get; set; }

		[LoadColumn(695)]
		public float C0693 { get; set; }

		[LoadColumn(696)]
		public float C0694 { get; set; }

		[LoadColumn(697)]
		public float C0695 { get; set; }

		[LoadColumn(698)]
		public float C0696 { get; set; }

		[LoadColumn(699)]
		public float C0697 { get; set; }

		[LoadColumn(700)]
		public float C0698 { get; set; }

		[LoadColumn(701)]
		public float C0699 { get; set; }

		[LoadColumn(702)]
		public float C0700 { get; set; }

		[LoadColumn(703)]
		public float C0701 { get; set; }

		[LoadColumn(704)]
		public float C0702 { get; set; }

		[LoadColumn(705)]
		public float C0703 { get; set; }

		[LoadColumn(706)]
		public float C0704 { get; set; }

		[LoadColumn(707)]
		public float C0705 { get; set; }

		[LoadColumn(708)]
		public float C0706 { get; set; }

		[LoadColumn(709)]
		public float C0707 { get; set; }

		[LoadColumn(710)]
		public float C0708 { get; set; }

		[LoadColumn(711)]
		public float C0709 { get; set; }

		[LoadColumn(712)]
		public float C0710 { get; set; }

		[LoadColumn(713)]
		public float C0711 { get; set; }

		[LoadColumn(714)]
		public float C0712 { get; set; }

		[LoadColumn(715)]
		public float C0713 { get; set; }

		[LoadColumn(716)]
		public float C0714 { get; set; }

		[LoadColumn(717)]
		public float C0715 { get; set; }

		[LoadColumn(718)]
		public float C0716 { get; set; }

		[LoadColumn(719)]
		public float C0717 { get; set; }

		[LoadColumn(720)]
		public float C0718 { get; set; }

		[LoadColumn(721)]
		public float C0719 { get; set; }

		[LoadColumn(722)]
		public float C0720 { get; set; }

		[LoadColumn(723)]
		public float C0721 { get; set; }

		[LoadColumn(724)]
		public float C0722 { get; set; }

		[LoadColumn(725)]
		public float C0723 { get; set; }

		[LoadColumn(726)]
		public float C0724 { get; set; }

		[LoadColumn(727)]
		public float C0725 { get; set; }

		[LoadColumn(728)]
		public float C0726 { get; set; }

		[LoadColumn(729)]
		public float C0727 { get; set; }

		[LoadColumn(730)]
		public float C0728 { get; set; }

		[LoadColumn(731)]
		public float C0729 { get; set; }

		[LoadColumn(732)]
		public float C0730 { get; set; }

		[LoadColumn(733)]
		public float C0731 { get; set; }

		[LoadColumn(734)]
		public float C0732 { get; set; }

		[LoadColumn(735)]
		public float C0733 { get; set; }

		[LoadColumn(736)]
		public float C0734 { get; set; }

		[LoadColumn(737)]
		public float C0735 { get; set; }

		[LoadColumn(738)]
		public float C0736 { get; set; }

		[LoadColumn(739)]
		public float C0737 { get; set; }

		[LoadColumn(740)]
		public float C0738 { get; set; }

		[LoadColumn(741)]
		public float C0739 { get; set; }

		[LoadColumn(742)]
		public float C0740 { get; set; }

		[LoadColumn(743)]
		public float C0741 { get; set; }

		[LoadColumn(744)]
		public float C0742 { get; set; }

		[LoadColumn(745)]
		public float C0743 { get; set; }

		[LoadColumn(746)]
		public float C0744 { get; set; }

		[LoadColumn(747)]
		public float C0745 { get; set; }

		[LoadColumn(748)]
		public float C0746 { get; set; }

		[LoadColumn(749)]
		public float C0747 { get; set; }

		[LoadColumn(750)]
		public float C0748 { get; set; }

		[LoadColumn(751)]
		public float C0749 { get; set; }

		[LoadColumn(752)]
		public float C0750 { get; set; }

		[LoadColumn(753)]
		public float C0751 { get; set; }

		[LoadColumn(754)]
		public float C0752 { get; set; }

		[LoadColumn(755)]
		public float C0753 { get; set; }

		[LoadColumn(756)]
		public float C0754 { get; set; }

		[LoadColumn(757)]
		public float C0755 { get; set; }

		[LoadColumn(758)]
		public float C0756 { get; set; }

		[LoadColumn(759)]
		public float C0757 { get; set; }

		[LoadColumn(760)]
		public float C0758 { get; set; }

		[LoadColumn(761)]
		public float C0759 { get; set; }

		[LoadColumn(762)]
		public float C0760 { get; set; }

		[LoadColumn(763)]
		public float C0761 { get; set; }

		[LoadColumn(764)]
		public float C0762 { get; set; }

		[LoadColumn(765)]
		public float C0763 { get; set; }

		[LoadColumn(766)]
		public float C0764 { get; set; }

		[LoadColumn(767)]
		public float C0765 { get; set; }

		[LoadColumn(768)]
		public float C0766 { get; set; }

		[LoadColumn(769)]
		public float C0767 { get; set; }

		[LoadColumn(770)]
		public float C0768 { get; set; }

		[LoadColumn(771)]
		public float C0769 { get; set; }

		[LoadColumn(772)]
		public float C0770 { get; set; }

		[LoadColumn(773)]
		public float C0771 { get; set; }

		[LoadColumn(774)]
		public float C0772 { get; set; }

		[LoadColumn(775)]
		public float C0773 { get; set; }

		[LoadColumn(776)]
		public float C0774 { get; set; }

		[LoadColumn(777)]
		public float C0775 { get; set; }

		[LoadColumn(778)]
		public float C0776 { get; set; }

		[LoadColumn(779)]
		public float C0777 { get; set; }

		[LoadColumn(780)]
		public float C0778 { get; set; }

		[LoadColumn(781)]
		public float C0779 { get; set; }

		[LoadColumn(782)]
		public float C0780 { get; set; }

		[LoadColumn(783)]
		public float C0781 { get; set; }

		[LoadColumn(784)]
		public float C0782 { get; set; }

		[LoadColumn(785)]
		public float C0783 { get; set; }

		[LoadColumn(786)]
		public float C0784 { get; set; }

		[LoadColumn(787)]
		public float C0785 { get; set; }

		[LoadColumn(788)]
		public float C0786 { get; set; }

		[LoadColumn(789)]
		public float C0787 { get; set; }

		[LoadColumn(790)]
		public float C0788 { get; set; }

		[LoadColumn(791)]
		public float C0789 { get; set; }

		[LoadColumn(792)]
		public float C0790 { get; set; }

		[LoadColumn(793)]
		public float C0791 { get; set; }

		[LoadColumn(794)]
		public float C0792 { get; set; }

		[LoadColumn(795)]
		public float C0793 { get; set; }

		[LoadColumn(796)]
		public float C0794 { get; set; }

		[LoadColumn(797)]
		public float C0795 { get; set; }

		[LoadColumn(798)]
		public float C0796 { get; set; }

		[LoadColumn(799)]
		public float C0797 { get; set; }

		[LoadColumn(800)]
		public float C0798 { get; set; }

		[LoadColumn(801)]
		public float C0799 { get; set; }

		[LoadColumn(802)]
		public float C0800 { get; set; }

		[LoadColumn(803)]
		public float C0801 { get; set; }

		[LoadColumn(804)]
		public float C0802 { get; set; }

		[LoadColumn(805)]
		public float C0803 { get; set; }

		[LoadColumn(806)]
		public float C0804 { get; set; }

		[LoadColumn(807)]
		public float C0805 { get; set; }

		[LoadColumn(808)]
		public float C0806 { get; set; }

		[LoadColumn(809)]
		public float C0807 { get; set; }

		[LoadColumn(810)]
		public float C0808 { get; set; }

		[LoadColumn(811)]
		public float C0809 { get; set; }

		[LoadColumn(812)]
		public float C0810 { get; set; }

		[LoadColumn(813)]
		public float C0811 { get; set; }

		[LoadColumn(814)]
		public float C0812 { get; set; }

		[LoadColumn(815)]
		public float C0813 { get; set; }

		[LoadColumn(816)]
		public float C0814 { get; set; }

		[LoadColumn(817)]
		public float C0815 { get; set; }

		[LoadColumn(818)]
		public float C0816 { get; set; }

		[LoadColumn(819)]
		public float C0817 { get; set; }

		[LoadColumn(820)]
		public float C0818 { get; set; }

		[LoadColumn(821)]
		public float C0819 { get; set; }

		[LoadColumn(822)]
		public float C0820 { get; set; }

		[LoadColumn(823)]
		public float C0821 { get; set; }

		[LoadColumn(824)]
		public float C0822 { get; set; }

		[LoadColumn(825)]
		public float C0823 { get; set; }

		[LoadColumn(826)]
		public float C0824 { get; set; }

		[LoadColumn(827)]
		public float C0825 { get; set; }

		[LoadColumn(828)]
		public float C0826 { get; set; }

		[LoadColumn(829)]
		public float C0827 { get; set; }

		[LoadColumn(830)]
		public float C0828 { get; set; }

		[LoadColumn(831)]
		public float C0829 { get; set; }

		[LoadColumn(832)]
		public float C0830 { get; set; }

		[LoadColumn(833)]
		public float C0831 { get; set; }

		[LoadColumn(834)]
		public float C0832 { get; set; }

		[LoadColumn(835)]
		public float C0833 { get; set; }

		[LoadColumn(836)]
		public float C0834 { get; set; }

		[LoadColumn(837)]
		public float C0835 { get; set; }

		[LoadColumn(838)]
		public float C0836 { get; set; }

		[LoadColumn(839)]
		public float C0837 { get; set; }

		[LoadColumn(840)]
		public float C0838 { get; set; }

		[LoadColumn(841)]
		public float C0839 { get; set; }

		[LoadColumn(842)]
		public float C0840 { get; set; }

		[LoadColumn(843)]
		public float C0841 { get; set; }

		[LoadColumn(844)]
		public float C0842 { get; set; }

		[LoadColumn(845)]
		public float C0843 { get; set; }

		[LoadColumn(846)]
		public float C0844 { get; set; }

		[LoadColumn(847)]
		public float C0845 { get; set; }

		[LoadColumn(848)]
		public float C0846 { get; set; }

		[LoadColumn(849)]
		public float C0847 { get; set; }

		[LoadColumn(850)]
		public float C0848 { get; set; }

		[LoadColumn(851)]
		public float C0849 { get; set; }

		[LoadColumn(852)]
		public float C0850 { get; set; }

		[LoadColumn(853)]
		public float C0851 { get; set; }

		[LoadColumn(854)]
		public float C0852 { get; set; }

		[LoadColumn(855)]
		public float C0853 { get; set; }

		[LoadColumn(856)]
		public float C0854 { get; set; }

		[LoadColumn(857)]
		public float C0855 { get; set; }

		[LoadColumn(858)]
		public float C0856 { get; set; }

		[LoadColumn(859)]
		public float C0857 { get; set; }

		[LoadColumn(860)]
		public float C0858 { get; set; }

		[LoadColumn(861)]
		public float C0859 { get; set; }

		[LoadColumn(862)]
		public float C0860 { get; set; }

		[LoadColumn(863)]
		public float C0861 { get; set; }

		[LoadColumn(864)]
		public float C0862 { get; set; }

		[LoadColumn(865)]
		public float C0863 { get; set; }

		[LoadColumn(866)]
		public float C0864 { get; set; }

		[LoadColumn(867)]
		public float C0865 { get; set; }

		[LoadColumn(868)]
		public float C0866 { get; set; }

		[LoadColumn(869)]
		public float C0867 { get; set; }

		[LoadColumn(870)]
		public float C0868 { get; set; }

		[LoadColumn(871)]
		public float C0869 { get; set; }

		[LoadColumn(872)]
		public float C0870 { get; set; }

		[LoadColumn(873)]
		public float C0871 { get; set; }

		[LoadColumn(874)]
		public float C0872 { get; set; }

		[LoadColumn(875)]
		public float C0873 { get; set; }

		[LoadColumn(876)]
		public float C0874 { get; set; }

		[LoadColumn(877)]
		public float C0875 { get; set; }

		[LoadColumn(878)]
		public float C0876 { get; set; }

		[LoadColumn(879)]
		public float C0877 { get; set; }

		[LoadColumn(880)]
		public float C0878 { get; set; }

		[LoadColumn(881)]
		public float C0879 { get; set; }

		[LoadColumn(882)]
		public float C0880 { get; set; }

		[LoadColumn(883)]
		public float C0881 { get; set; }

		[LoadColumn(884)]
		public float C0882 { get; set; }

		[LoadColumn(885)]
		public float C0883 { get; set; }

		[LoadColumn(886)]
		public float C0884 { get; set; }

		[LoadColumn(887)]
		public float C0885 { get; set; }

		[LoadColumn(888)]
		public float C0886 { get; set; }

		[LoadColumn(889)]
		public float C0887 { get; set; }

		[LoadColumn(890)]
		public float C0888 { get; set; }

		[LoadColumn(891)]
		public float C0889 { get; set; }

		[LoadColumn(892)]
		public float C0890 { get; set; }

		[LoadColumn(893)]
		public float C0891 { get; set; }

		[LoadColumn(894)]
		public float C0892 { get; set; }

		[LoadColumn(895)]
		public float C0893 { get; set; }

		[LoadColumn(896)]
		public float C0894 { get; set; }

		[LoadColumn(897)]
		public float C0895 { get; set; }

		[LoadColumn(898)]
		public float C0896 { get; set; }

		[LoadColumn(899)]
		public float C0897 { get; set; }

		[LoadColumn(900)]
		public float C0898 { get; set; }

		[LoadColumn(901)]
		public float C0899 { get; set; }

		[LoadColumn(902)]
		public float C0900 { get; set; }

		[LoadColumn(903)]
		public float C0901 { get; set; }

		[LoadColumn(904)]
		public float C0902 { get; set; }

		[LoadColumn(905)]
		public float C0903 { get; set; }

		[LoadColumn(906)]
		public float C0904 { get; set; }

		[LoadColumn(907)]
		public float C0905 { get; set; }

		[LoadColumn(908)]
		public float C0906 { get; set; }

		[LoadColumn(909)]
		public float C0907 { get; set; }

		[LoadColumn(910)]
		public float C0908 { get; set; }

		[LoadColumn(911)]
		public float C0909 { get; set; }

		[LoadColumn(912)]
		public float C0910 { get; set; }

		[LoadColumn(913)]
		public float C0911 { get; set; }

		[LoadColumn(914)]
		public float C0912 { get; set; }

		[LoadColumn(915)]
		public float C0913 { get; set; }

		[LoadColumn(916)]
		public float C0914 { get; set; }

		[LoadColumn(917)]
		public float C0915 { get; set; }

		[LoadColumn(918)]
		public float C0916 { get; set; }

		[LoadColumn(919)]
		public float C0917 { get; set; }

		[LoadColumn(920)]
		public float C0918 { get; set; }

		[LoadColumn(921)]
		public float C0919 { get; set; }

		[LoadColumn(922)]
		public float C0920 { get; set; }

		[LoadColumn(923)]
		public float C0921 { get; set; }

		[LoadColumn(924)]
		public float C0922 { get; set; }

		[LoadColumn(925)]
		public float C0923 { get; set; }

		[LoadColumn(926)]
		public float C0924 { get; set; }

		[LoadColumn(927)]
		public float C0925 { get; set; }

		[LoadColumn(928)]
		public float C0926 { get; set; }

		[LoadColumn(929)]
		public float C0927 { get; set; }

		[LoadColumn(930)]
		public float C0928 { get; set; }

		[LoadColumn(931)]
		public float C0929 { get; set; }

		[LoadColumn(932)]
		public float C0930 { get; set; }

		[LoadColumn(933)]
		public float C0931 { get; set; }

		[LoadColumn(934)]
		public float C0932 { get; set; }

		[LoadColumn(935)]
		public float C0933 { get; set; }

		[LoadColumn(936)]
		public float C0934 { get; set; }

		[LoadColumn(937)]
		public float C0935 { get; set; }

		[LoadColumn(938)]
		public float C0936 { get; set; }

		[LoadColumn(939)]
		public float C0937 { get; set; }

		[LoadColumn(940)]
		public float C0938 { get; set; }

		[LoadColumn(941)]
		public float C0939 { get; set; }

		[LoadColumn(942)]
		public float C0940 { get; set; }

		[LoadColumn(943)]
		public float C0941 { get; set; }

		[LoadColumn(944)]
		public float C0942 { get; set; }

		[LoadColumn(945)]
		public float C0943 { get; set; }

		[LoadColumn(946)]
		public float C0944 { get; set; }

		[LoadColumn(947)]
		public float C0945 { get; set; }

		[LoadColumn(948)]
		public float C0946 { get; set; }

		[LoadColumn(949)]
		public float C0947 { get; set; }

		[LoadColumn(950)]
		public float C0948 { get; set; }

		[LoadColumn(951)]
		public float C0949 { get; set; }

		[LoadColumn(952)]
		public float C0950 { get; set; }

		[LoadColumn(953)]
		public float C0951 { get; set; }

		[LoadColumn(954)]
		public float C0952 { get; set; }

		[LoadColumn(955)]
		public float C0953 { get; set; }

		[LoadColumn(956)]
		public float C0954 { get; set; }

		[LoadColumn(957)]
		public float C0955 { get; set; }

		[LoadColumn(958)]
		public float C0956 { get; set; }

		[LoadColumn(959)]
		public float C0957 { get; set; }

		[LoadColumn(960)]
		public float C0958 { get; set; }

		[LoadColumn(961)]
		public float C0959 { get; set; }

		[LoadColumn(962)]
		public float C0960 { get; set; }

		[LoadColumn(963)]
		public float C0961 { get; set; }

		[LoadColumn(964)]
		public float C0962 { get; set; }

		[LoadColumn(965)]
		public float C0963 { get; set; }

		[LoadColumn(966)]
		public float C0964 { get; set; }

		[LoadColumn(967)]
		public float C0965 { get; set; }

		[LoadColumn(968)]
		public float C0966 { get; set; }

		[LoadColumn(969)]
		public float C0967 { get; set; }

		[LoadColumn(970)]
		public float C0968 { get; set; }

		[LoadColumn(971)]
		public float C0969 { get; set; }

		[LoadColumn(972)]
		public float C0970 { get; set; }

		[LoadColumn(973)]
		public float C0971 { get; set; }

		[LoadColumn(974)]
		public float C0972 { get; set; }

		[LoadColumn(975)]
		public float C0973 { get; set; }

		[LoadColumn(976)]
		public float C0974 { get; set; }

		[LoadColumn(977)]
		public float C0975 { get; set; }

		[LoadColumn(978)]
		public float C0976 { get; set; }

		[LoadColumn(979)]
		public float C0977 { get; set; }

		[LoadColumn(980)]
		public float C0978 { get; set; }

		[LoadColumn(981)]
		public float C0979 { get; set; }

		[LoadColumn(982)]
		public float C0980 { get; set; }

		[LoadColumn(983)]
		public float C0981 { get; set; }

		[LoadColumn(984)]
		public float C0982 { get; set; }

		[LoadColumn(985)]
		public float C0983 { get; set; }

		[LoadColumn(986)]
		public float C0984 { get; set; }

		[LoadColumn(987)]
		public float C0985 { get; set; }

		[LoadColumn(988)]
		public float C0986 { get; set; }

		[LoadColumn(989)]
		public float C0987 { get; set; }

		[LoadColumn(990)]
		public float C0988 { get; set; }

		[LoadColumn(991)]
		public float C0989 { get; set; }

		[LoadColumn(992)]
		public float C0990 { get; set; }

		[LoadColumn(993)]
		public float C0991 { get; set; }

		[LoadColumn(994)]
		public float C0992 { get; set; }

		[LoadColumn(995)]
		public float C0993 { get; set; }

		[LoadColumn(996)]
		public float C0994 { get; set; }

		[LoadColumn(997)]
		public float C0995 { get; set; }

		[LoadColumn(998)]
		public float C0996 { get; set; }

		[LoadColumn(999)]
		public float C0997 { get; set; }

		[LoadColumn(1000)]
		public float C0998 { get; set; }

		[LoadColumn(1001)]
		public float C0999 { get; set; }

	}
}